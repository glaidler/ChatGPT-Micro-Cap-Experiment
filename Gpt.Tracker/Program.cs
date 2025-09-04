// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IMarketData>(new PolygonMarketData("gvP7MQ_yp7p6Ozs5YwjzM8sKYlfQtUJL"));
                services.AddSingleton<OpenAiDecisionEngine>(sp =>
                {
                    var config = context.Configuration;
                    var model = config["OpenAi:Model"] ?? "gpt-4o-mini";
                    return new OpenAiDecisionEngine(model);
                });
                services.AddSingleton<IConfiguration>(sp => context.Configuration);
            });

        var host = builder.Build();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var opts = CliOptions.Parse(args);

        var projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
        var dataDir = Path.Combine(projectRoot ?? AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);

        var portfolioPath = Path.Combine(dataDir, "portfolio.csv");
        var tradesPath = Path.Combine(dataDir, "trades.csv");
        var equityPath = Path.Combine(dataDir, "equity.csv");

        var portfolio = File.Exists(portfolioPath)
            ? CsvIo.LoadPortfolio(portfolioPath)
            : new Portfolio { Cash = 100m }; // default £/$100 starting equity like original project

        var market = host.Services.GetRequiredService<IMarketData>();
        var today = opts.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // 1) Refresh prices & apply stop-losses
        await RefreshPricesAndStops(portfolio, market, today, tradesPath, opts);

        // 2) Optionally get AI decisions (buys/sells) and execute
        if (opts.EnableAi)
        {
            var ai = host.Services.GetRequiredService<OpenAiDecisionEngine>();
            var decisions = await ai.GetDecisionsAsync(portfolio, today, opts.WeeklyDeepResearch);
            ExecuteDecisions(portfolio, decisions, tradesPath, today);
        }

        // 3) Mark-to-market and write equity curve
        var totalEquity = await MarkToMarketAsync(portfolio, market, today);
        CsvIo.SavePortfolio(portfolioPath, portfolio);
        CsvIo.AppendEquity(equityPath, today, totalEquity);

        Console.WriteLine($"[{today}] Equity: {totalEquity:C}  Cash: {portfolio.Cash:C}");
        return 0;
    }

    private static async Task RefreshPricesAndStops(Portfolio p, IMarketData market, DateOnly asOf, string tradesPath, CliOptions opts)
    {
        foreach (var h in p.Holdings.ToList())
        {
            var last = await market.GetLastCloseAsync(h.Symbol, asOf);
            h.LastClose = last;

            if (h.StopLossPercent is not null)
            {
                var stop = h.AvgPrice * (1 - (decimal)h.StopLossPercent!.Value / 100m);
                if (last <= stop && h.Shares > 0)
                {
                    // Sell at market/close
                    var proceeds = h.Shares * last;
                    p.Cash += proceeds;
                    var trade = new Trade
                    {
                        Date = asOf,
                        Symbol = h.Symbol,
                        Side = "SELL",
                        Quantity = h.Shares,
                        Price = last,
                        Reason = "STOP_LOSS"
                    };
                    CsvIo.AppendTrade(tradesPath, trade);
                    // clear holding
                    h.Shares = 0;
                    h.AvgPrice = 0;
                }
            }
        }

        // Remove empty lines
        p.Holdings = p.Holdings.Where(x => x.Shares > 0).ToList();
    }

    private static void ExecuteDecisions(Portfolio p, AiDecision decisions, string tradesPath, DateOnly asOf)
    {
        if (decisions == null) return;

        // Sells first
        foreach (var s in decisions.Sells ?? Enumerable.Empty<AiOrder>())
        {
            var h = p.Holdings.FirstOrDefault(x => x.Symbol.Equals(s.Symbol, StringComparison.OrdinalIgnoreCase));
            if (h == null || h.Shares <= 0) continue;

            var qty = Math.Min(h.Shares, s.Quantity > 0 ? s.Quantity : h.Shares);
            var price = h.LastClose > 0 ? h.LastClose : h.AvgPrice; // fall back

            var proceeds = qty * price;
            p.Cash += proceeds;

            var trade = new Trade
            {
                Date = asOf,
                Symbol = h.Symbol,
                Side = "SELL",
                Quantity = qty,
                Price = price,
                Reason = "AI_DECISION"
            };
            CsvIo.AppendTrade(tradesPath, trade);

            h.Shares -= qty;
            if (h.Shares == 0) h.AvgPrice = 0;
        }
        p.Holdings = p.Holdings.Where(x => x.Shares > 0).ToList();

        // Buys next (respect cash)
        foreach (var b in decisions.Buys ?? Enumerable.Empty<AiOrder>())
        {
            // Enforce micro-cap rule if provided
            if (decisions.MicroCapOnly && (b.MarketCapUsd is decimal mc) && mc > 300_000_000m)
                continue;

            var price = b.AssumedPrice ?? 0m;
            if (price <= 0m)
            {
                // If no price provided by AI, try last known price in portfolio (if any)
                var existing = p.Holdings.FirstOrDefault(x => x.Symbol.Equals(b.Symbol, StringComparison.OrdinalIgnoreCase));
                price = existing?.LastClose ?? 0m;
            }
            if (price <= 0m) continue; // skip if no price

            var cost = b.Quantity * price;
            if (cost > p.Cash) continue; // skip if not enough cash

            p.Cash -= cost;
            var holding = p.Holdings.FirstOrDefault(x => x.Symbol.Equals(b.Symbol, StringComparison.OrdinalIgnoreCase));
            if (holding == null)
            {
                holding = new Holding
                {
                    Symbol = b.Symbol.ToUpperInvariant(),
                    Shares = b.Quantity,
                    AvgPrice = price,
                    StopLossPercent = b.StopLossPercent,
                    LastClose = price
                };
                p.Holdings.Add(holding);
            }
            else
            {
                // adjust average price
                var totalCost = holding.AvgPrice * holding.Shares + cost;
                holding.Shares += b.Quantity;
                holding.AvgPrice = totalCost / holding.Shares;
                if (b.StopLossPercent is not null) holding.StopLossPercent = b.StopLossPercent;
                holding.LastClose = price;
            }

            var trade = new Trade
            {
                Date = asOf,
                Symbol = holding.Symbol,
                Side = "BUY",
                Quantity = b.Quantity,
                Price = price,
                Reason = "AI_DECISION"
            };
            CsvIo.AppendTrade(tradesPath, trade);
        }
    }

    private static async Task<decimal> MarkToMarketAsync(Portfolio p, IMarketData market, DateOnly asOf)
    {
        decimal equity = p.Cash;
        foreach (var h in p.Holdings)
        {
            if (h.LastClose <= 0)
                h.LastClose = await market.GetLastCloseAsync(h.Symbol, asOf);

            equity += h.Shares * h.LastClose;
        }
        return equity;
    }
}
