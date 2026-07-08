namespace NickeltownFinance.Core.DTOs;

public class LegacyTreasurerMonthSummary
{
    public string SheetName { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public decimal OpeningBankBalance { get; set; }
    public decimal ClosingBankBalance { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal ShireBonds { get; set; }
    public decimal PayPalBalance { get; set; }
    public int IncomeLineCount { get; set; }
    public int ExpenseLineCount { get; set; }
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
}

public class LegacyTreasurerParseResult
{
    public string FileName { get; set; } = string.Empty;
    public IReadOnlyList<LegacyTreasurerMonthSummary> Months { get; set; } = [];
    public IReadOnlyList<ImportPreviewRow> Rows { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class LegacyTreasurerAnalyseResult
{
    public LegacyTreasurerParseResult? Preview { get; set; }
    public string? FailureReason { get; set; }
}

public class LegacyTreasurerCommitRequest
{
    public string FileName { get; set; } = string.Empty;
    public IReadOnlyList<ImportPreviewRow> Rows { get; set; } = [];
    public IReadOnlyList<LegacyTreasurerMonthSummary> Months { get; set; } = [];
}
