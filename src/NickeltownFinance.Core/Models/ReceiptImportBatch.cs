using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

/// <summary>
/// Groups multiple receipt imports from one desktop/scanner session for undo.
/// </summary>
public class ReceiptImportBatch : BaseEntity
{
    public ReceiptImportSource Source { get; set; } = ReceiptImportSource.Desktop;

    public string Label { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public int ItemCount { get; set; }

    public int CommittedCount { get; set; }

    public bool IsUndone { get; set; }

    public ObjectId ImportedByUserId { get; set; } = ObjectId.Empty;

    public string ImportedByName { get; set; } = string.Empty;
}
