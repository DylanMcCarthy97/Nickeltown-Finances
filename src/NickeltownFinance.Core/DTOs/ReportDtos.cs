using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Formatting;

namespace NickeltownFinance.Core.DTOs;

public class TransactionListItem
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string CategoryColour { get; set; } = "#1565C0";

    public decimal IncomeAmount { get; set; }

    public decimal ExpenseAmount { get; set; }

    public decimal DisplayAmount => IncomeAmount > 0 ? IncomeAmount : ExpenseAmount;

    public bool IsIncome => IncomeAmount > 0;

    public string PaymentMethod { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public decimal RunningBalance { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public string LastModified { get; set; } = string.Empty;

    public DateTime ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }

    public bool HasReceipt { get; set; }

    public int AttachmentCount { get; set; }

    public string? ThumbnailPath { get; set; }

    public string ReceiptStatusText => ReceiptRequired ? "Receipt required"
        : HasReceipt ? $"{AttachmentCount} attachment(s)"
        : "No receipt";

    public ObjectId CategoryId { get; set; } = ObjectId.Empty;

    /// <summary>True for ANZ Square transfer credits.</summary>
    public bool IsSquareDeposit { get; set; }

    /// <summary>True when a matched Square deposit breakdown is available.</summary>
    public bool HasSquareDepositDetail { get; set; }

    public ObjectId? SquareDepositId { get; set; }

    public bool IsSquareAwaitingMatch => IsSquareDeposit && !HasSquareDepositDetail;

    public string SquareStatusToolTip => HasSquareDepositDetail
        ? "Square deposit matched — click to view breakdown"
        : IsSquareAwaitingMatch
            ? "Awaiting Square match — import Square transactions CSV"
            : string.Empty;

    /// <summary>True for expenses that still need a receipt attached.</summary>
    public bool ReceiptRequired => ExpenseAmount > 0 && !HasReceipt;
}



public class ChartMonthItem
{
    public string Label { get; set; } = string.Empty;

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Profit => Income - Expenses;

    public double IncomeBarHeight { get; set; }

    public double ExpenseBarHeight { get; set; }

    public double ProfitBarHeight { get; set; }
}

public class DashboardSummary
{
    public decimal BankBalance { get; set; }

    public decimal IncomeThisMonth { get; set; }

    public decimal ExpensesThisMonth { get; set; }

    public decimal ProfitThisMonth { get; set; }

    public decimal CurrentProfitLoss { get; set; }

    public decimal YearToDateIncome { get; set; }

    public decimal YearToDateExpenses { get; set; }

    public decimal YearToDateProfit { get; set; }

    public int TransactionCount { get; set; }

    public string CurrentFinancialYearName { get; set; } = string.Empty;

    public DateTime FinancialYearEndDate { get; set; }

    public int DaysUntilYearEnd { get; set; }

    public IReadOnlyList<TransactionListItem> RecentTransactions { get; set; } = [];

    public IReadOnlyList<ChartMonthItem> MonthlyChartData { get; set; } = [];

    public IReadOnlyList<string> RecentActivity { get; set; } = [];

    public int ReceiptsAttached { get; set; }

    public int MissingReceipts { get; set; }

    public ImportStatusSummary ImportStatus { get; set; } = new();
}


public class CategoryTotal
{
    public string CategoryName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

/// <summary>One bank transaction line for treasurer reports (ANZ description + category).</summary>
public class ReportTransactionLine
{
    public DateTime Date { get; set; }

    /// <summary>Bank statement description (what ANZ shows).</summary>
    public string Description { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public decimal MoneyIn { get; set; }

    public decimal MoneyOut { get; set; }

    public string MoneyInDisplay => MoneyIn > 0 ? MoneyIn.ToString("C") : "—";

    public string MoneyOutDisplay => MoneyOut > 0 ? MoneyOut.ToString("C") : "—";

    /// <summary>Square line-item breakdown when this bank row is a matched Square deposit.</summary>
    public IReadOnlyList<SquareBreakdownLine> SquareItems { get; set; } = [];

    public bool HasSquareItems => SquareItems.Count > 0;
}

public class SquareBreakdownLine
{
    public string Section { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Amount { get; set; }

    public string Label => Quantity > 1 ? $"{Quantity} × {ItemName}" : ItemName;

    public string AmountDisplay => Amount > 0 ? Amount.ToString("C") : "—";
}

public class SquareBreakdownSection
{
    public string SectionName { get; set; } = string.Empty;

    public IReadOnlyList<SquareBreakdownLine> Items { get; set; } = [];

    public decimal SectionTotal { get; set; }
}

public class MonthDocumentInfo
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public MonthDocumentKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

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

    public string DisplayLabel => string.IsNullOrWhiteSpace(Title) ? FileName : Title;
}

public class MonthlyReportData
{
    public string ClubName { get; set; } = string.Empty;

    public string? LogoPath { get; set; }

    public string FinancialYearName { get; set; } = string.Empty;

    public string MonthName { get; set; } = string.Empty;

    public int Month { get; set; }

    public int Year { get; set; }

    public string PeriodLabel => $"{MonthName} {Year}";

    public decimal OpeningBalance { get; set; }

    public IReadOnlyList<CategoryTotal> IncomeByCategory { get; set; } = [];

    public decimal TotalIncome { get; set; }

    public IReadOnlyList<CategoryTotal> ExpensesByCategory { get; set; } = [];

    public decimal TotalExpenses { get; set; }

    public decimal MonthlyProfit { get; set; }

    public decimal ClosingBalance { get; set; }

    /// <summary>Calendar date the closing balance was calculated for (print date, capped at month end).</summary>
    public DateTime ClosingBalanceAsOf { get; set; }

    public string ClosingBalanceTitle =>
        $"Closing balance (as at {DateFormats.Format(ClosingBalanceAsOf)})";

    public decimal CashOnHand { get; set; }

    public decimal ShireBonds { get; set; }

    public decimal PayPalBalance { get; set; }

    public decimal OtherFundsTotal => CashOnHand + ShireBonds + PayPalBalance;

    public decimal TotalFundsOwned => ClosingBalance + OtherFundsTotal;

    /// <summary>Individual transactions with bank descriptions and categories.</summary>
    public IReadOnlyList<ReportTransactionLine> Transactions { get; set; } = [];

    /// <summary>Aggregated Square sales behind matched bank deposits this month.</summary>
    public IReadOnlyList<SquareBreakdownSection> SquareBreakdown { get; set; } = [];

    public bool HasSquareBreakdown => SquareBreakdown.Count > 0;

    /// <summary>Pitstop end-of-day reports attached to this calendar month.</summary>
    public IReadOnlyList<MonthDocumentInfo> PitstopReports { get; set; } = [];

    public bool HasPitstopReports => PitstopReports.Count > 0;

    public int TransactionCount => Transactions.Count;

    public string Notes { get; set; } = string.Empty;

    /// <summary>Treasurer (or admin) who generated/printed the report.</summary>
    public string PreparedBy { get; set; } = string.Empty;

    public string PreparedByRole { get; set; } = "Treasurer";

    public DateTime PrintedAt { get; set; } = DateTime.Now;

    public string PrintedAtDisplay => DateFormats.FormatDateTime(PrintedAt);

    /// <summary>Optional digital signature image path for PDF export.</summary>
    public string? SignatureImagePath { get; set; }

    public bool HasSignature =>
        !string.IsNullOrWhiteSpace(SignatureImagePath) && File.Exists(SignatureImagePath);
}

public class MonthlyBreakdown
{
    public string MonthName { get; set; } = string.Empty;

    public int Month { get; set; }

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Profit => Income - Expenses;
}

public class AgmReportData
{
    public string ClubName { get; set; } = string.Empty;

    public string? LogoPath { get; set; }

    public string FinancialYearName { get; set; } = string.Empty;

    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalance { get; set; }

    /// <summary>Last day of the financial year covered by this report.</summary>
    public DateTime FinancialYearEndDate { get; set; }

    /// <summary>Calendar date the closing balance was calculated for (print date, capped at year end).</summary>
    public DateTime ClosingBalanceAsOf { get; set; }

    public string ClosingBalanceTitle =>
        $"Closing balance (as at {DateFormats.Format(ClosingBalanceAsOf)})";

    public decimal CashOnHand { get; set; }

    public decimal ShireBonds { get; set; }

    public decimal PayPalBalance { get; set; }

    public decimal OtherFundsTotal => CashOnHand + ShireBonds + PayPalBalance;

    public decimal TotalFundsOwned => ClosingBalance + OtherFundsTotal;

    public decimal TotalIncome { get; set; }

    public decimal TotalExpenses { get; set; }

    public decimal AnnualProfit { get; set; }

    public IReadOnlyList<MonthlyBreakdown> MonthlyData { get; set; } = [];

    public IReadOnlyList<CategoryTotal> IncomeByCategory { get; set; } = [];

    public IReadOnlyList<CategoryTotal> ExpensesByCategory { get; set; } = [];

    public string PreparedBy { get; set; } = string.Empty;

    public string PreparedByRole { get; set; } = "Treasurer";

    public DateTime PrintedAt { get; set; } = DateTime.Now;

    public string PrintedAtDisplay => DateFormats.FormatDateTime(PrintedAt);

    /// <summary>Optional digital signature image path for PDF export.</summary>
    public string? SignatureImagePath { get; set; }

    public bool HasSignature =>
        !string.IsNullOrWhiteSpace(SignatureImagePath) && File.Exists(SignatureImagePath);
}

public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public long SizeBytes { get; set; }

    public string SizeDisplay => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:0.0} KB"
        : $"{SizeBytes / (1024.0 * 1024.0):0.00} MB";
}
