using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

public class StooqMarketData : IMarketData
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    });

    public async Task<decimal> GetLastCloseAsync(string symbol, DateOnly asOf)
    {
        // Try US suffix first; stooq daily CSV: https://stooq.com/q/d/l/?s=aapl.us&i=d
        var s = symbol.ToLowerInvariant().EndsWith(".us") ? symbol.ToLowerInvariant() : symbol.ToLowerInvariant() + ".us";
        var url = $"https://stooq.com/q/d/l/?s={WebUtility.UrlEncode(s)}&i=d";
        var csv = await Http.GetStringAsync(url);
        // CSV columns: Date,Open,High,Low,Close,Volume
        // Take last row <= asOf
        using var sr = new StringReader(csv);
        var header = await sr.ReadLineAsync(); // skip
        string? line;
        decimal last = 0m;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            var parts = line.Split(',');
            if (parts.Length < 6) continue;
            if (!DateOnly.TryParse(parts[0], out var d)) continue;
            if (d > asOf) break;
            if (decimal.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var close))
                last = close;
        }
        return last; // 0 if not found -> caller can decide fallback
    }
}