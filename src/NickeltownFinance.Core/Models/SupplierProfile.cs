using LiteDB;

namespace NickeltownFinance.Core.Models;

/// <summary>
/// Learned supplier profile with cached statistics for fast dashboard rendering.
/// </summary>
public class SupplierProfile : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public List<string> KnownAbns { get; set; } = [];

    public List<string> Aliases { get; set; } = [];

    public string? Website { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Future: relative path under AppData for supplier logo.</summary>
    public string? LogoRelativePath { get; set; }

    public ObjectId? DefaultCategoryId { get; set; }

    public string? DefaultCategoryName { get; set; }

    public bool DefaultGstRegistered { get; set; } = true;

    public string? DefaultPaymentMethod { get; set; }

    public string? DefaultTaxTreatment { get; set; }

    public string? DefaultNotes { get; set; }

    public ObjectId? PreferredAccountId { get; set; }

    public byte Confidence { get; set; }

    public List<string> KnownKeywords { get; set; } = [];

    public List<string> KnownInvoiceFormats { get; set; } = [];

    public DateTime? FirstPurchaseDate { get; set; }

    public DateTime? LastPurchaseDate { get; set; }

    public int PurchaseCount { get; set; }

    public decimal AverageSpend { get; set; }

    public decimal LargestSpend { get; set; }

    public decimal SmallestSpend { get; set; }

    public decimal TotalSpent { get; set; }

    // --- Cached analytics (updated incrementally on commit) ---

    public DateTime? StatisticsCachedAtUtc { get; set; }

    public decimal CurrentFinancialYearSpend { get; set; }

    public decimal PreviousFinancialYearSpend { get; set; }

    public decimal Rolling12MonthSpend { get; set; }

    public decimal LifetimeGstPaid { get; set; }

    public decimal AverageMonthlySpend { get; set; }

    public double AverageDaysBetweenPurchases { get; set; }

    // --- Supplier health ---

    public byte ReceiptQualityScore { get; set; } = 70;

    public byte OcrAccuracyScore { get; set; } = 70;

    public byte CategoryAccuracyScore { get; set; } = 70;

    public byte MatchAccuracyScore { get; set; } = 70;

    public int DuplicateCount { get; set; }
}
