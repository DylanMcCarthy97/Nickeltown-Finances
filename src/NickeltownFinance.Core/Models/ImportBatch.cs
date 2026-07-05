using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class ImportBatch : BaseEntity
{
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public string FileName { get; set; } = string.Empty;

    public StatementFormat SourceFormat { get; set; }

    public ImportSourceType SourceType { get; set; } = ImportSourceType.AnzBank;

    public string BankName { get; set; } = "ANZ";

    public int TransactionsImported { get; set; }

    public int TransactionsSkipped { get; set; }

    public int DuplicatesDetected { get; set; }

    public int Errors { get; set; }

    public int SquareMatched { get; set; }

    public int SquareNeedsReview { get; set; }

    public double ProcessingTimeMs { get; set; }

    public ObjectId UserId { get; set; } = ObjectId.Empty;

    public string UserName { get; set; } = string.Empty;

    public bool IsUndone { get; set; }

    public List<string> ErrorMessages { get; set; } = [];
}

