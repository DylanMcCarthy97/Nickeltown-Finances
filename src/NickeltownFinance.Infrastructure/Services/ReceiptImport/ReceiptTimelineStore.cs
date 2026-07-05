using System.Text.Json;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public static class ReceiptTimelineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static List<ReceiptTimelineEvent> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ReceiptTimelineEvent>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<ReceiptTimelineEvent> events) =>
        JsonSerializer.Serialize(events, JsonOptions);

    public static void AddEvent(
        ReceiptImportItem item,
        string stage,
        string? userName = null,
        string? detail = null,
        int? durationMs = null,
        byte? confidence = null)
    {
        var events = Parse(item.TimelineJson);
        events.Add(new ReceiptTimelineEvent
        {
            Stage = stage,
            TimestampUtc = DateTime.UtcNow,
            UserName = userName,
            Detail = detail,
            DurationMs = durationMs,
            Confidence = confidence
        });
        item.TimelineJson = Serialize(events);
    }

    public static void ResetTimeline(ReceiptImportItem item) =>
        item.TimelineJson = null;
}
