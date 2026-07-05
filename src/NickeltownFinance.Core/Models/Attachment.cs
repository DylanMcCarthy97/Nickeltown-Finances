using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class Attachment : BaseEntity
{
    public ObjectId TransactionId { get; set; } = ObjectId.Empty;

    public AttachmentKind Kind { get; set; } = AttachmentKind.Receipt;

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? ThumbnailRelativePath { get; set; }

    public int PageCount { get; set; } = 1;

    /// <summary>JSON array of preview page relative paths.</summary>
    public string? PreviewPagesJson { get; set; }

    public ObjectId AddedByUserId { get; set; } = ObjectId.Empty;

    public string AddedByName { get; set; } = string.Empty;

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    // OCR fields (populated by future OCR; never overwrite user-entered transaction data)
    public bool OcrProcessed { get; set; }

    public string? OcrSupplier { get; set; }

    public DateTime? OcrDate { get; set; }

    public decimal? OcrGst { get; set; }

    public decimal? OcrTotal { get; set; }

    public string? OcrAbn { get; set; }

    public ObjectId? OcrSuggestedCategoryId { get; set; }

    public string? OcrInvoiceNumber { get; set; }

    public decimal? OcrSubtotal { get; set; }

    public string? OcrPaymentMethod { get; set; }

    public byte? OcrSupplierConfidence { get; set; }

    public byte? OcrDateConfidence { get; set; }

    public byte? OcrCategoryConfidence { get; set; }

    /// <summary>SHA-256 hex of stored file.</summary>
    public string? FileHash { get; set; }

    public ReceiptImportSource? UploadSource { get; set; }

    public string? UploadDeviceName { get; set; }

    /// <summary>Original file before image processing (camera uploads).</summary>
    public string? OriginalRelativePath { get; set; }

    /// <summary>Internal OCR-optimised image (ocr.jpg).</summary>
    public string? OcrImageRelativePath { get; set; }
}
