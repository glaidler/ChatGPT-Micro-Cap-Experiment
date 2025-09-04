public class Trade
{
    public DateOnly Date { get; set; }
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = ""; // BUY/SELL
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Reason { get; set; } = ""; // STOP_LOSS or AI_DECISION
}