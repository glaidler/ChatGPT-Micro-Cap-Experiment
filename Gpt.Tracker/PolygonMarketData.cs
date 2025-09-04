using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class PolygonMarketData : IMarketData
{
    private readonly string _apiKey;
    private static readonly HttpClient Http = new();

    public PolygonMarketData(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    public async Task<decimal> GetLastCloseAsync(string symbol, DateOnly asOf)
    {
        // Polygon.io expects US stocks as e.g. "AAPL"
        var dateStr = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"https://api.polygon.io/v1/open-close/{symbol.ToUpperInvariant()}/{dateStr}?adjusted=true&apiKey={_apiKey}";

        var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Polygon returns { "close": 123.45, ... }
        if (root.TryGetProperty("close", out var closeProp) && closeProp.TryGetDecimal(out var close))
            return close;

        return 0m; // fallback if not found
    }
}