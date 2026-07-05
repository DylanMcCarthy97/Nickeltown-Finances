using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public CategoryType Type { get; set; }

    public string Colour { get; set; } = "#1565C0";

    /// <summary>Fluent UI symbol name, e.g. "Money24", "Cart24".</summary>
    public string Icon { get; set; } = "Tag24";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Archived categories are hidden from new entry but retained on history.</summary>
    public bool IsArchived { get; set; }
}
