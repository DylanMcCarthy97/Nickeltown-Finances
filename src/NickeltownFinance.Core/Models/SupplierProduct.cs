using LiteDB;

namespace NickeltownFinance.Core.Models;

public class SupplierProduct : BaseEntity
{
    public ObjectId SupplierProfileId { get; set; } = ObjectId.Empty;

    public string NormalizedKey { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public decimal LastPrice { get; set; }

    public decimal HighestPrice { get; set; }

    public decimal LowestPrice { get; set; }

    public decimal AveragePrice { get; set; }

    public int PurchaseCount { get; set; }

    public DateTime? LastPurchasedDate { get; set; }

    public List<SupplierProductPricePoint> PriceHistory { get; set; } = [];
}

public class SupplierProductPricePoint
{
    public DateTime Date { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; } = 1;

    public decimal Total { get; set; }

    public ObjectId? PurchaseId { get; set; }
}
