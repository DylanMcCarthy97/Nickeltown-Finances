using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.DTOs;

public class ParsedStatementRow
{
    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal? Balance { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;
}

public class StatementParseResult
{
    public StatementFormat Format { get; set; }

    public string BankName { get; set; } = "ANZ";

    public string FileName { get; set; } = string.Empty;

    public IReadOnlyList<ParsedStatementRow> Rows { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class ImportPreviewRow
{
    public bool IsSelected { get; set; } = true;

    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal? Balance { get; set; }

    public string Reference { get; set; } = string.Empty;

    public ObjectId? SuggestedCategoryId { get; set; }

    public string SuggestedCategoryName { get; set; } = string.Empty;

    public bool IsSquareDeposit { get; set; }

    /// <summary>When true, save a categorisation rule for this description.</summary>
    public bool RememberCategory { get; set; }

    public ImportRowStatus Status { get; set; } = ImportRowStatus.New;

    public ObjectId? MatchedTransactionId { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public double DuplicateConfidence { get; set; }

    public string? Error { get; set; }
}

public class ImportPreview
{
    public string FileName { get; set; } = string.Empty;

    public StatementFormat Format { get; set; }

    public string BankName { get; set; } = "ANZ";

    public IReadOnlyList<ImportPreviewRow> Rows { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public int NewCount => Rows.Count(r => r.Status == ImportRowStatus.New);

    public int DuplicateCount => Rows.Count(r => r.Status == ImportRowStatus.Duplicate);

    public int MatchedCount => Rows.Count(r => r.Status == ImportRowStatus.Matched);
}

public class ImportCommitRequest
{
    public string FileName { get; set; } = string.Empty;

    public StatementFormat Format { get; set; }

    public string BankName { get; set; } = "ANZ";

    public IReadOnlyList<ImportPreviewRow> Rows { get; set; } = [];
}

public class ImportResult
{
    public int Imported { get; set; }

    public int Skipped { get; set; }

    public int Duplicates { get; set; }

    public int Errors { get; set; }

    public int SquareMatched { get; set; }

    public int SquareNeedsReview { get; set; }

    public double ProcessingTimeMs { get; set; }

    public ObjectId BatchId { get; set; } = ObjectId.Empty;

    public IReadOnlyList<string> ErrorMessages { get; set; } = [];
}

public class ImportHistoryItem
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public DateTime ImportedAt { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourceFormat { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public int TransactionsImported { get; set; }

    public int TransactionsSkipped { get; set; }

    public int DuplicatesDetected { get; set; }

    public int Errors { get; set; }

    public double ProcessingTimeMs { get; set; }

    public string UserName { get; set; } = string.Empty;

    public bool UndoAvailable { get; set; }

    public bool IsUndone { get; set; }
}

public class SquarePreviewRow
{
    public bool IsSelected { get; set; } = true;

    public string DepositId { get; set; } = string.Empty;

    public DateTime DepositDate { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal Fees { get; set; }

    public decimal NetAmount { get; set; }

    public int TransactionCount { get; set; }

    public ImportRowStatus Status { get; set; } = ImportRowStatus.New;

    public string Fingerprint { get; set; } = string.Empty;

    public IReadOnlyList<SquareTransactionPreviewLine> Lines { get; set; } = [];
}

public class SquareTransactionPreviewLine
{
    public DateTime Date { get; set; }

    public DateTime TransactionTime { get; set; }

    public DateTime? DepositDate { get; set; }

    public string ExternalDepositId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PaymentId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }

    public decimal Fees { get; set; }

    public decimal NetAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string ExternalTransactionId { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;
}

public class SquareImportPreview
{
    public string FileName { get; set; } = string.Empty;

    public IReadOnlyList<SquarePreviewRow> Deposits { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class SquareImportAnalyseResult
{
    public SquareImportPreview? Preview { get; set; }

    public string? FailureReason { get; set; }
}

public class SquareImportCommitRequest
{
    public string FileName { get; set; } = string.Empty;

    public IReadOnlyList<SquarePreviewRow> Deposits { get; set; } = [];
}

public class ReconciliationSummary
{
    public int BankDeposits { get; set; }

    public int Matched { get; set; }

    public int NeedsReview { get; set; }

    public int UnmatchedAnz { get; set; }

    public int UnmatchedSquare { get; set; }

    public int DuplicateTransactions { get; set; }

    public int ImportErrors { get; set; }

    public decimal ReconciledTotal { get; set; }

    public IReadOnlyList<ReconciliationMatchItem> MatchedDeposits { get; set; } = [];

    public IReadOnlyList<ReconciliationAnzItem> UnmatchedAnzDeposits { get; set; } = [];

    public IReadOnlyList<ReconciliationSquareItem> UnmatchedSquareDeposits { get; set; } = [];

    public IReadOnlyList<ReconciliationMatchCandidate> ManualReviewItems { get; set; } = [];
}

public class ReconciliationAnzItem
{
    public ObjectId TransactionId { get; set; } = ObjectId.Empty;

    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Reference { get; set; } = string.Empty;
}

public class ReconciliationSquareItem
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public string DepositId { get; set; } = string.Empty;

    public DateTime DepositDate { get; set; }

    public decimal NetAmount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal Fees { get; set; }

    public int TransactionCount { get; set; }

    public string Status { get; set; } = string.Empty;
}

public class ReconciliationMatchItem
{
    public ObjectId TransactionId { get; set; } = ObjectId.Empty;

    public ObjectId SquareDepositId { get; set; } = ObjectId.Empty;

    public DateTime BankDate { get; set; }

    public DateTime DepositDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string DepositId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public class ReconciliationMatchCandidate
{
    public ReconciliationAnzItem AnzDeposit { get; set; } = new();

    public IReadOnlyList<ReconciliationSquareItem> PossibleSquareDeposits { get; set; } = [];
}

public class ImportStatusSummary
{
    public int RecentImportCount { get; set; }

    public int TransactionsWaitingReview { get; set; }

    public int SquareDepositsWaitingMatch { get; set; }

    public int DuplicatesFound { get; set; }

    public DateTime? LastBankImport { get; set; }

    public DateTime? LastSquareImport { get; set; }

    public IReadOnlyList<ImportHistoryItem> RecentImports { get; set; } = [];
}

public class AttachmentInfo
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public ObjectId TransactionId { get; set; } = ObjectId.Empty;

    public AttachmentKind Kind { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public string? ThumbnailFullPath { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string SizeDisplay => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:0.0} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):0.00} MB";

    public DateTime DateAdded { get; set; }

    public string AddedByName { get; set; } = string.Empty;

    public bool IsImage { get; set; }

    public bool IsPdf { get; set; }

    public int PageCount { get; set; } = 1;

    public IReadOnlyList<string> PreviewFullPaths { get; set; } = [];

    public string? OcrImageFullPath { get; set; }

    public string? DisplayPreviewPath =>
        PreviewFullPaths.FirstOrDefault()
        ?? ThumbnailFullPath
        ?? (IsImage ? FullPath : null);
}

public class OcrExtractionResult
{
    public string? Supplier { get; set; }

    public byte? SupplierConfidence { get; set; }

    public DateTime? Date { get; set; }

    public byte? DateConfidence { get; set; }

    public string? InvoiceNumber { get; set; }

    public byte? InvoiceNumberConfidence { get; set; }

    public decimal? Gst { get; set; }

    public byte? GstConfidence { get; set; }

    public decimal? Subtotal { get; set; }

    public byte? SubtotalConfidence { get; set; }

    public decimal? Total { get; set; }

    public byte? TotalConfidence { get; set; }

    public string? Abn { get; set; }

    public byte? AbnConfidence { get; set; }

    public string? PaymentMethod { get; set; }

    public byte? PaymentMethodConfidence { get; set; }

    public string? FullText { get; set; }

    public string? Currency { get; set; }

    public byte? CurrencyConfidence { get; set; }

    public ObjectId? SuggestedCategoryId { get; set; }

    public byte? CategoryConfidence { get; set; }
}

public class TransactionSearchFilter
{
    public string? Search { get; set; }

    public ObjectId? CategoryId { get; set; }

    public bool? HasReceipt { get; set; }

    public AttachmentKind? ReceiptType { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    /// <summary>When true, only income; when false, only expenses; null = both.</summary>
    public bool? IsIncome { get; set; }

    public bool IncludeDeleted { get; set; }
}

public class CategorisationRuleItem
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public string MatchText { get; set; } = string.Empty;

    public ObjectId CategoryId { get; set; } = ObjectId.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string ActionDisplay { get; set; } = "Category";

    public CategorisationRuleAction Action { get; set; }

    public int Priority { get; set; }

    public bool IsSystem { get; set; }

    public int HitCount { get; set; }

    public DateTime LastUsedUtc { get; set; }
}

public class CategorisationSuggestion
{
    public ObjectId? CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public bool IsSquareDeposit { get; set; }
}

public class SquareDepositDetailDto
{
    public DateTime DepositDate { get; set; }

    public decimal GrossSales { get; set; }

    public decimal Fees { get; set; }

    public decimal NetDeposit { get; set; }

    public IReadOnlyList<SquareDepositItemGroupDto> Groups { get; set; } = [];
}

public class SquareDepositItemGroupDto
{
    public string Description { get; set; } = string.Empty;

    public string DisplayTitle { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public IReadOnlyList<SquareDepositLineItemDto> Items { get; set; } = [];
}

public class SquareDepositLineItemDto
{
    public string Description { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime TransactionTime { get; set; }

    public string? PaymentId { get; set; }
}
