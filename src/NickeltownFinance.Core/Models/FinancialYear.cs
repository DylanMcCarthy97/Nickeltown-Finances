namespace NickeltownFinance.Core.Models;

/// <summary>
/// A financial year with a user-entered starting balance and an auto-calculated ledger opening balance.
/// Current bank balance = OpeningBalance + income − expenses.
/// </summary>
public class FinancialYear : BaseEntity
{
    /// <summary>Financial year label, e.g. "2024-25".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>First day of the financial year period (e.g. 1 July).</summary>
    public DateTime OpeningDate { get; set; }

    /// <summary>Inclusive last day of the financial year period.</summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Date the club began tracking in this app (may be mid-year).
    /// The user enters their bank balance as of the end of this day.
    /// </summary>
    public DateTime StartingDate { get; set; }

    /// <summary>
    /// Bank balance at the end of <see cref="StartingDate"/>, as entered by the user.
    /// Transactions on this date are already included and do not change the current balance.
    /// Never edited by imports — only by the treasurer.
    /// </summary>
    public decimal StartingBalance { get; set; }

    /// <summary>
    /// Ledger opening balance used by reports and running balances.
    /// Auto-calculated: StartingBalance − net of transactions on or before StartingDate.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>Only one financial year may be active at a time.</summary>
    public bool IsActive { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Locked years cannot accept new transactions or imports.</summary>
    public bool IsLocked { get; set; }

    public string Notes { get; set; } = string.Empty;
}
