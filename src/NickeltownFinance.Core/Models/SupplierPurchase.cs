using LiteDB;

namespace NickeltownFinance.Core.Models;

public class SupplierPurchase : BaseEntity
{
    public ObjectId SupplierProfileId { get; set; } = ObjectId.Empty;

    public ObjectId? TransactionId { get; set; }

    public ObjectId? ReceiptImportItemId { get; set; }

    public ObjectId? AttachmentId { get; set; }

    public ObjectId FinancialYearId { get; set; } = ObjectId.Empty;

    public DateTime PurchaseDate { get; set; }

    public string? InvoiceNumber { get; set; }

    public decimal Total { get; set; }

    public decimal Gst { get; set; }

    public ObjectId? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? PaymentMethod { get; set; }

    public bool MatchedToBank { get; set; }

    public string? MatchedTransactionDescription { get; set; }

    public string? ThumbnailRelativePath { get; set; }

    public string ReceiptStatus { get; set; } = "Committed";

    public List<SupplierPurchaseLineItem> LineItems { get; set; } = [];
}

public class SupplierPurchaseLineItem
{
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal Total { get; set; }

    public decimal Gst { get; set; }

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public ObjectId? ProductId { get; set; }
}
