using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class AuditLogEntry
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public ObjectId? ActorUserId { get; set; }

    public string ActorUsername { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    public ObjectId? TargetUserId { get; set; }

    public string TargetUsername { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}
