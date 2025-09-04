using System;

public class CliOptions
{
    public DateOnly? AsOfDate { get; set; }
    public bool EnableAi { get; set; } = true;
    public bool WeeklyDeepResearch { get; set; } = false;
    public string? OpenAiModel { get; set; }

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--asof" when i + 1 < args.Length:
                    if (DateOnly.TryParse(args[++i], out var d)) o.AsOfDate = d;
                    break;
                case "--no-ai":
                    o.EnableAi = false; break;
                case "--weekly":
                    o.WeeklyDeepResearch = true; break;
                case "--model" when i + 1 < args.Length:
                    o.OpenAiModel = args[++i]; break;
            }
        }
        return o;
    }
}