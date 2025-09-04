using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public static class CsvIo
{
    private static readonly string[] PortfolioHeader = ["Symbol", "Shares", "AvgPrice", "StopLossPercent", "LastClose", "Cash"];
    private static readonly string[] TradeHeader = ["Date", "Symbol", "Side", "Quantity", "Price", "Reason"];
    private static readonly string[] EquityHeader = ["Date", "Equity"];

    public static Portfolio LoadPortfolio(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length <= 1) return new Portfolio();
        var p = new Portfolio();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = SplitCsv(line);
            if (cols.Length < 6) continue;
            var sym = cols[0];
            if (sym.Equals("CASH", StringComparison.OrdinalIgnoreCase))
                p.Cash = ParseDec(cols[5]);
            else
            {
                p.Holdings.Add(new Holding
                {
                    Symbol = sym,
                    Shares = ParseInt(cols[1]),
                    AvgPrice = ParseDec(cols[2]),
                    StopLossPercent = ParseNullableDouble(cols[3]),
                    LastClose = ParseDec(cols[4])
                });
                p.Cash = ParseDec(cols[5]); // same cash on each line; harmless
            }
        }
        return p;
    }

    public static void SavePortfolio(string path, Portfolio p)
    {
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine(string.Join(',', PortfolioHeader));
        foreach (var h in p.Holdings)
        {
            sw.WriteLine(string.Join(',', new[]
            {
                h.Symbol,
                h.Shares.ToString(CultureInfo.InvariantCulture),
                h.AvgPrice.ToString(CultureInfo.InvariantCulture),
                h.StopLossPercent?.ToString(CultureInfo.InvariantCulture) ?? "",
                h.LastClose.ToString(CultureInfo.InvariantCulture),
                p.Cash.ToString(CultureInfo.InvariantCulture)
            }));
        }
        // Ensure we have a CASH row even with zero holdings
        if (p.Holdings.Count == 0)
        {
            sw.WriteLine(string.Join(',', new[]
            {
                "CASH","0","0","","0",p.Cash.ToString(CultureInfo.InvariantCulture)
            }));
        }
    }

    public static void AppendTrade(string path, Trade t)
    {
        var exists = File.Exists(path);
        using var sw = new StreamWriter(path, true, Encoding.UTF8);
        if (!exists) sw.WriteLine(string.Join(',', TradeHeader));
        sw.WriteLine(string.Join(',', new[]
        {
            t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            t.Symbol,
            t.Side,
            t.Quantity.ToString(CultureInfo.InvariantCulture),
            t.Price.ToString(CultureInfo.InvariantCulture),
            t.Reason
        }));
    }

    public static void AppendEquity(string path, DateOnly date, decimal equity)
    {
        var exists = File.Exists(path);
        using var sw = new StreamWriter(path, true, Encoding.UTF8);
        if (!exists) sw.WriteLine(string.Join(',', EquityHeader));
        sw.WriteLine($"{date:yyyy-MM-dd},{equity.ToString(CultureInfo.InvariantCulture)}");
    }

    private static string[] SplitCsv(string line)
        => line.Split(',').Select(s => s.Trim()).ToArray();

    private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static decimal ParseDec(string s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    private static double? ParseNullableDouble(string s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}