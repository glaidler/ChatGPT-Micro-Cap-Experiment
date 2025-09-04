using ServiceStack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAiDecisionEngine
{
    private readonly string _model;
    private readonly JsonApiClient _client;

    public OpenAiDecisionEngine(string model)
    {
        _model = model;
        var apiKey = "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";
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Set OPENAI_API_KEY environment variable.");

        _client = new JsonApiClient("https://api.openai.com/v1")
        {
            BearerToken = apiKey
        };
    }

    public async Task<AiDecision> GetDecisionsAsync(Portfolio p, DateOnly asOf, bool weeklyDeepResearch)
    {
        var sys = "You are a professional portfolio strategist trading ONLY U.S.-listed micro-cap stocks (<$300M cap). " +
                  "Return pure JSON with fields: microCapOnly, buys[], sells[]. Buys include symbol, quantity, assumedPrice (if known), stopLossPercent, marketCapUsd. " +
                  "Sells include symbol and quantity. Keep portfolio 3-4 names, allow full turnover weekly.";

        var dataSummary = BuildDataSummary(p, asOf);

        var user = new StringBuilder();
        user.AppendLine($"As-Of: {asOf:yyyy-MM-dd}");
        if (!p.Holdings.Any())
            user.AppendLine("Current Portfolio is empty.");
        else
        {
            user.AppendLine("Current Portfolio (symbol, shares, avgPrice, lastClose, stopLoss%):");
        }

        foreach (var h in p.Holdings)
            user.AppendLine($"- {h.Symbol},{h.Shares},{h.AvgPrice},{h.LastClose},{h.StopLossPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""}");
        user.AppendLine($"Cash: {p.Cash}");
        if (weeklyDeepResearch)
            user.AppendLine("Deep research allowed today. You may fully rebalance.");
        user.AppendLine("Return ONLY JSON. Example: {\"microCapOnly\":true,\"buys\":[{\"symbol\":\"ABCD\",\"quantity\":2,\"assumedPrice\":3.21,\"stopLossPercent\":8.0,\"marketCapUsd\":120000000}],\"sells\":[{\"symbol\":\"WXYZ\",\"quantity\":1}]}");

        // OpenAI Chat Completions with ServiceStack client
        var req = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = sys },
                new { role = "user", content = user.ToString() },
                new { role = "user", content = "Market Data Summary:\n" + dataSummary }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        // POST /chat/completions
        var res = await _client.PostAsync<string>("/chat/completions", req);
        var deserialised = res.FromJson<AiResponse>();
        var decision = deserialised?.Choices?.FirstOrDefault()?.Message?.Content.FromJson<AiDecision>() ?? new AiDecision(){MicroCapOnly = true};
        return decision;
    }

    private string BuildDataSummary(Portfolio p, DateOnly asOf)
    {
        // Keep it short; you can extend to include benchmarks, volumes, etc.
        var sb = new StringBuilder();
        sb.AppendLine($"As-Of {asOf:yyyy-MM-dd}, summarize holdings:");
        foreach (var h in p.Holdings)
            sb.AppendLine($"{h.Symbol}: lastClose={h.LastClose}, avgPrice={h.AvgPrice}, stopLoss%={h.StopLossPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
        return sb.ToString();
    }
}