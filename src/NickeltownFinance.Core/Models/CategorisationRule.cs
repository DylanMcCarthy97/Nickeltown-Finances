using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class CategorisationRule : BaseEntity
{
    /// <summary>Case-insensitive substring matched against the bank description.</summary>
    public string MatchText { get; set; } = string.Empty;

    public ObjectId CategoryId { get; set; } = ObjectId.Empty;

    public CategorisationRuleAction Action { get; set; } = CategorisationRuleAction.AssignCategory;

    /// <summary>Higher priority wins when multiple rules match.</summary>
    public int Priority { get; set; }

    /// <summary>Seeded defaults; still editable by the treasurer.</summary>
    public bool IsSystem { get; set; }

    public int HitCount { get; set; }

    public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
}

