using LiteDB;

namespace NickeltownFinance.Core.Models;

public class TreasurerMonthSnapshot : BaseEntity
{
    public int Year { get; set; }

    public int Month { get; set; }

    public DateTime? PeriodFrom { get; set; }

    public DateTime? PeriodTo { get; set; }

    public decimal OpeningBankBalance { get; set; }

    public decimal ClosingBankBalance { get; set; }

    public decimal CashOnHand { get; set; }

    public decimal ShireBonds { get; set; }

    public decimal PayPalBalance { get; set; }

    public string SheetName { get; set; } = string.Empty;

    public ObjectId? ImportBatchId { get; set; }
}
