namespace NickeltownFinance.Core.Models;

public class ReceiptTimelineEvent
{
    public string Stage { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string? UserName { get; set; }

    public string? Detail { get; set; }

    public int? DurationMs { get; set; }

    public byte? Confidence { get; set; }
}
