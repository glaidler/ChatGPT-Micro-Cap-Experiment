public class Holding
{
    public string Symbol { get; set; } = "";
    public int Shares { get; set; }
    public decimal AvgPrice { get; set; }
    public double? StopLossPercent { get; set; } // e.g., 8.0
    public decimal LastClose { get; set; }
}