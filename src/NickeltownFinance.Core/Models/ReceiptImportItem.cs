using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

/// <summary>
/// A receipt in the import inbox before it is attached to a transaction.
/// Flow: upload → preprocess → OCR → AI → match → user commit.
/// </summary>
public class ReceiptImportItem : BaseEntity
{
    public ReceiptImportStatus Status { get; set; } = ReceiptImportStatus.Queued;

    public ReceiptImportSource Source { get; set; } = ReceiptImportSource.Desktop;

    public string FileName { get; set; } = string.Empty;

    /// <summary>Original uploaded file relative to AppData root.</summary>
    public string OriginalRelativePath { get; set; } = string.Empty;

    /// <summary>Colour-enhanced preview (preview.jpg) for user viewing.</summary>
    public string? ProcessedRelativePath { get; set; }

    /// <summary>Internal OCR-optimised image (ocr.jpg).</summary>
    public string? OcrImageRelativePath { get; set; }

    public string? ThumbnailRelativePath { get; set; }

    public int PageCount { get; set; } = 1;

    /// <summary>JSON array of preview page relative paths (page1.jpg, page2.jpg, …).</summary>
    public string? PreviewPagesJson { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hex of original file bytes.</summary>
    public string FileHash { get; set; } = string.Empty;

    public string? UploadDeviceName { get; set; }

    public string? UploadDeviceUserAgent { get; set; }

    public ObjectId? ImportBatchId { get; set; }

    /// <summary>Auto-attach to this transaction when processing completes (mobile upload from editor).</summary>
    public ObjectId? PendingTransactionId { get; set; }

    /// <summary>Links uploads to an editor or desktop mobile-upload session.</summary>
    public string? UploadSessionKey { get; set; }

    public ReceiptImportTarget ImportTarget { get; set; } = ReceiptImportTarget.Inbox;

    public bool HasProcessingWarnings { get; set; }

    public bool AttachedDespiteProcessingFailure { get; set; }

    /// <summary>Set after user commits Match.</summary>
    public ObjectId? MatchedTransactionId { get; set; }

    /// <summary>Attachment created when item is committed.</summary>
    public ObjectId? ResultAttachmentId { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    // --- OCR extraction (suggestions only until commit) ---

    public bool OcrProcessed { get; set; }

    public string? OcrSupplier { get; set; }

    public byte? OcrSupplierConfidence { get; set; }

    public DateTime? OcrDate { get; set; }

    public byte? OcrDateConfidence { get; set; }

    public string? OcrInvoiceNumber { get; set; }

    public byte? OcrInvoiceNumberConfidence { get; set; }

    public string? OcrAbn { get; set; }

    public byte? OcrAbnConfidence { get; set; }

    public decimal? OcrSubtotal { get; set; }

    public byte? OcrSubtotalConfidence { get; set; }

    public decimal? OcrGst { get; set; }

    public byte? OcrGstConfidence { get; set; }

    public decimal? OcrTotal { get; set; }

    public byte? OcrTotalConfidence { get; set; }

    public string? OcrPaymentMethod { get; set; }

    public byte? OcrPaymentMethodConfidence { get; set; }

    public string? OcrFullText { get; set; }

    public string? OcrCurrency { get; set; }

    public byte? OcrCurrencyConfidence { get; set; }

    // --- User corrections (corrected value wins everywhere) ---

    public string? CorrectedSupplier { get; set; }

    public DateTime? CorrectedDate { get; set; }

    public string? CorrectedInvoiceNumber { get; set; }

    public string? CorrectedAbn { get; set; }

    public decimal? CorrectedSubtotal { get; set; }

    public decimal? CorrectedGst { get; set; }

    public decimal? CorrectedTotal { get; set; }

    public string? CorrectedPaymentMethod { get; set; }

    // --- Supplier detection ---

    public ObjectId? DetectedSupplierProfileId { get; set; }

    public string? DetectedSupplierName { get; set; }

    public byte? DetectedSupplierConfidence { get; set; }

    // --- AI category suggestion ---

    public ObjectId? AiSuggestedCategoryId { get; set; }

    public string? AiSuggestedCategoryName { get; set; }

    public byte? AiCategoryConfidence { get; set; }

    // --- Transaction match suggestion ---

    public ObjectId? SuggestedMatchTransactionId { get; set; }

    public byte? SuggestedMatchConfidence { get; set; }

    public string? SuggestedMatchDescription { get; set; }

    public DateTime? SuggestedMatchDate { get; set; }

    public decimal? SuggestedMatchAmount { get; set; }

    public ReceiptMatchQuality MatchQuality { get; set; } = ReceiptMatchQuality.None;

    public bool IsPossibleDuplicate { get; set; }

    public string? DuplicateWarning { get; set; }

    /// <summary>JSON array of <see cref="ReceiptTimelineEvent"/>.</summary>
    public string? TimelineJson { get; set; }
}
