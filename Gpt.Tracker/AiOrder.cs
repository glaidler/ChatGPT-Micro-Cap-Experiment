public class AiOrder
{
    public string Symbol { get; set; } = "";
    public int Quantity { get; set; }
    public decimal? AssumedPrice { get; set; }
    public double? StopLossPercent { get; set; }
    public decimal? MarketCapUsd { get; set; }
}