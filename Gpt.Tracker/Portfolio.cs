using System.Collections.Generic;

public class Portfolio
{
    public decimal Cash { get; set; }
    public List<Holding> Holdings { get; set; } = new();
}