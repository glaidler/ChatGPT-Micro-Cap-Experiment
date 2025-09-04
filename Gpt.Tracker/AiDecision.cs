using System.Collections.Generic;

public class AiDecision
{
    public bool MicroCapOnly { get; set; } = true;
    public List<AiOrder>? Buys { get; set; }
    public List<AiOrder>? Sells { get; set; }
}