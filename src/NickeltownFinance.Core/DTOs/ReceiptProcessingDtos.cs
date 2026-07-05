using LiteDB;

namespace NickeltownFinance.Core.DTOs;

public class ReceiptOcrCorrectionRequest
{
    public ObjectId ImportItemId { get; set; } = ObjectId.Empty;

    public string? Supplier { get; set; }

    public DateTime? Date { get; set; }

    public string? InvoiceNumber { get; set; }

    public string? Abn { get; set; }

    public decimal? Subtotal { get; set; }

    public decimal? Gst { get; set; }

    public decimal? Total { get; set; }

    public string? PaymentMethod { get; set; }
}

public class ReceiptProcessingSettingsInfo
{
    public bool AutoImageEnhancement { get; set; } = true;

    public bool OcrEnabled { get; set; } = true;

    public bool AiCategorisation { get; set; } = true;

    public bool BankMatching { get; set; } = true;

    public bool DuplicateDetection { get; set; } = true;

    public bool ThumbnailGeneration { get; set; } = true;
}

public class SupplierDetectionResult
{
    public ObjectId? ProfileId { get; set; }

    public string? SupplierName { get; set; }

    public byte Confidence { get; set; }

    public ObjectId? SuggestedCategoryId { get; set; }

    public string? SuggestedCategoryName { get; set; }

    public string? DefaultPaymentMethod { get; set; }

    public bool DefaultGstRegistered { get; set; } = true;
}

public class ReceiptDuplicateCheckResult
{
    public bool IsPossibleDuplicate { get; set; }

    public string? Warning { get; set; }

    public ObjectId? ExistingItemId { get; set; }
}

public class ReceiptProcessingLogEntry
{
    public ObjectId ImportItemId { get; set; } = ObjectId.Empty;

    public string Stage { get; set; } = string.Empty;

    public string Event { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public int? DurationMs { get; set; }

    public byte? Confidence { get; set; }

    public string? Error { get; set; }
}
