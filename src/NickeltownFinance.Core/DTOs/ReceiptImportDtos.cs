using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.DTOs;

public class ReceiptImportItemInfo
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public ReceiptImportStatus Status { get; set; }

    public ReceiptImportSource Source { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string OriginalFullPath { get; set; } = string.Empty;

    public string? ProcessedFullPath { get; set; }

    public string? ThumbnailFullPath { get; set; }

    public int PageCount { get; set; } = 1;

    public IReadOnlyList<string> PreviewFullPaths { get; set; } = [];

    public string? OcrImageFullPath { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedDate { get; set; }

    public string? UploadDeviceName { get; set; }

    public string? ErrorMessage { get; set; }

    public ReceiptOcrInfo? Ocr { get; set; }

    public ReceiptAiSuggestionInfo? AiSuggestion { get; set; }

    public ReceiptMatchSuggestionInfo? MatchSuggestion { get; set; }

    public ReceiptMatchQuality MatchQuality { get; set; }

    public bool IsPossibleDuplicate { get; set; }

    public string? DuplicateWarning { get; set; }

    public IReadOnlyList<ReceiptTimelineEvent> Timeline { get; set; } = [];

    public string? DetectedSupplierName { get; set; }

    public byte? DetectedSupplierConfidence { get; set; }

    public ObjectId? PendingTransactionId { get; set; }

    public string? UploadSessionKey { get; set; }

    public ReceiptImportTarget ImportTarget { get; set; } = ReceiptImportTarget.Inbox;

    public bool HasProcessingWarnings { get; set; }

    public bool AttachedDespiteProcessingFailure { get; set; }

    public string StatusDisplay => Status switch
    {
        ReceiptImportStatus.Queued => "Queued",
        ReceiptImportStatus.Uploading => "Uploading…",
        ReceiptImportStatus.Preprocessing => "Processing image…",
        ReceiptImportStatus.ProcessingOcr => "Processing OCR…",
        ReceiptImportStatus.SupplierDetection => "Detecting supplier…",
        ReceiptImportStatus.AiParsing => "Analysing…",
        ReceiptImportStatus.MatchingTransaction => "Matching transaction…",
        ReceiptImportStatus.GeneratingThumbnail => "Generating thumbnail…",
        ReceiptImportStatus.Ready => "Ready for review",
        ReceiptImportStatus.CompletedWithWarnings => "Completed with warnings",
        ReceiptImportStatus.Failed => "Failed",
        ReceiptImportStatus.Committed when AttachedDespiteProcessingFailure => "Attached — processing failed, original saved",
        ReceiptImportStatus.Committed => "Attached",
        ReceiptImportStatus.Ignored => "Ignored",
        _ => Status.ToString()
    };
}

public class ReceiptOcrInfo
{
    public string? Supplier { get; set; }

    public byte? SupplierConfidence { get; set; }

    public string? CorrectedSupplier { get; set; }

    public string? EffectiveSupplier => CorrectedSupplier ?? Supplier;

    public DateTime? Date { get; set; }

    public byte? DateConfidence { get; set; }

    public DateTime? CorrectedDate { get; set; }

    public DateTime? EffectiveDate => CorrectedDate ?? Date;

    public string? InvoiceNumber { get; set; }

    public byte? InvoiceNumberConfidence { get; set; }

    public string? CorrectedInvoiceNumber { get; set; }

    public string? EffectiveInvoiceNumber => CorrectedInvoiceNumber ?? InvoiceNumber;

    public string? Abn { get; set; }

    public byte? AbnConfidence { get; set; }

    public string? CorrectedAbn { get; set; }

    public string? EffectiveAbn => CorrectedAbn ?? Abn;

    public decimal? Subtotal { get; set; }

    public byte? SubtotalConfidence { get; set; }

    public decimal? CorrectedSubtotal { get; set; }

    public decimal? EffectiveSubtotal => CorrectedSubtotal ?? Subtotal;

    public decimal? Gst { get; set; }

    public byte? GstConfidence { get; set; }

    public decimal? CorrectedGst { get; set; }

    public decimal? EffectiveGst => CorrectedGst ?? Gst;

    public decimal? Total { get; set; }

    public byte? TotalConfidence { get; set; }

    public decimal? CorrectedTotal { get; set; }

    public decimal? EffectiveTotal => CorrectedTotal ?? Total;

    public string? PaymentMethod { get; set; }

    public byte? PaymentMethodConfidence { get; set; }

    public string? CorrectedPaymentMethod { get; set; }

    public string? EffectivePaymentMethod => CorrectedPaymentMethod ?? PaymentMethod;

    public string? FullText { get; set; }

    public string? Currency { get; set; }

    public byte? CurrencyConfidence { get; set; }
}

public class ReceiptAiSuggestionInfo
{
    public ObjectId? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public byte? Confidence { get; set; }
}

public class ReceiptMatchSuggestionInfo
{
    public ObjectId TransactionId { get; set; } = ObjectId.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public decimal Amount { get; set; }

    public byte Confidence { get; set; }

    public ReceiptMatchQuality Quality { get; set; }

    public string QualityDisplay => Quality switch
    {
        ReceiptMatchQuality.Excellent => "Excellent Match",
        ReceiptMatchQuality.Likely => "Likely Match",
        ReceiptMatchQuality.Possible => "Possible Match",
        _ => "No Match"
    };

    public bool IsExcellentHighlight => Confidence >= 97;
}

public class MobileUploadSessionInfo
{
    public string Token { get; set; } = string.Empty;

    public string UploadUrl { get; set; } = string.Empty;

    public string QrPayload { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int Port { get; set; }

    public string LocalIpAddress { get; set; } = string.Empty;

    public string? SessionKey { get; set; }

    public ObjectId? TargetTransactionId { get; set; }

    public ReceiptImportTarget ImportTarget { get; set; } = ReceiptImportTarget.Inbox;
}

public class MobileUploadSessionRequest
{
    public ObjectId? TargetTransactionId { get; set; }

    public string? SessionKey { get; set; }

    public ReceiptImportTarget ImportTarget { get; set; } = ReceiptImportTarget.Inbox;
}

public class ReceiptUploadStatusInfo
{
    public string ImportItemId { get; set; } = string.Empty;

    public bool UploadSucceeded { get; set; } = true;

    public string Status { get; set; } = string.Empty;

    public string StatusDisplay { get; set; } = string.Empty;

    public string? Stage { get; set; }

    public string? ErrorMessage { get; set; }

    public bool ProcessingComplete { get; set; }

    public bool ProcessingFailed { get; set; }

    public bool CanRetry { get; set; }

    public string? Supplier { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string ImportTarget { get; set; } = "Inbox";

    public bool IsAttached { get; set; }
}

public class ReceiptImportCommitRequest
{
    public ObjectId ImportItemId { get; set; } = ObjectId.Empty;

    public ReceiptMatchAction Action { get; set; }

    /// <summary>When Action is Match, the transaction to attach to (defaults to suggestion).</summary>
    public ObjectId? TransactionId { get; set; }

    /// <summary>When Action is CreateNew, optional overrides for the new transaction.</summary>
    public ObjectId? CategoryId { get; set; }

    public DateTime? Date { get; set; }

    public decimal? Amount { get; set; }

    public string? Description { get; set; }

    public AttachmentKind AttachmentKind { get; set; } = AttachmentKind.Receipt;
}

public class ReceiptImageProcessRequest
{
    public string SourceFilePath { get; set; } = string.Empty;

    public string OutputFilePath { get; set; } = string.Empty;

    public int? ManualRotationDegrees { get; set; }

    /// <summary>Optional four corner points for manual crop (x,y pairs).</summary>
    public int[]? ManualCropCorners { get; set; }
}

public class ReceiptImageProcessResult
{
    public bool Success { get; set; }

    public string OutputFilePath { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public class ScannerDeviceInfo
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool SupportsAdf { get; set; }

    public bool SupportsDuplex { get; set; }
}

public class ScannerCaptureRequest
{
    public string DeviceId { get; set; } = string.Empty;

    public int Dpi { get; set; } = 300;

    public bool UseAdf { get; set; }

    public bool Duplex { get; set; }

    public bool MultiPage { get; set; }
}

public class ScannerCaptureResult
{
    public IReadOnlyList<string> OutputFilePaths { get; set; } = Array.Empty<string>();

    public int PageCount { get; set; }
}
