using LiteDB;

namespace NickeltownFinance.Core.DTOs;

public class FinancialYearListItem
{
    public ObjectId Id { get; set; } = ObjectId.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime OpeningDate { get; set; }

    public DateTime StartingDate { get; set; }

    public decimal StartingBalance { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; }

    public bool IsArchived { get; set; }

    public bool IsLocked { get; set; }

    public string Status { get; set; } = string.Empty;

    public string StatusKind { get; set; } = "Inactive";

    public bool CanDelete { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public class CreateFinancialYearRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>First day of the financial year period.</summary>
    public DateTime OpeningDate { get; set; }

    /// <summary>Date tracking begins (defaults to OpeningDate when not set).</summary>
    public DateTime? StartingDate { get; set; }

    /// <summary>Bank balance on the starting date (user-entered).</summary>
    public decimal StartingBalance { get; set; }

    /// <summary>Legacy alias — maps to StartingBalance.</summary>
    public decimal OpeningBalance
    {
        get => StartingBalance;
        set => StartingBalance = value;
    }

    public bool CarryForwardPreviousClosingBalance { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public class FirstRunSetupRequest
{
    public string ClubName { get; set; } = string.Empty;

    /// <summary>Month the financial year starts (1–12). Default July.</summary>
    public int FinancialYearStartMonth { get; set; } = 7;

    /// <summary>Date tracking begins — bank balance is as of this date.</summary>
    public DateTime StartingDate { get; set; }

    /// <summary>Bank balance on the starting date.</summary>
    public decimal StartingBalance { get; set; }

    /// <summary>Legacy alias — maps to StartingBalance.</summary>
    public decimal OpeningBalance
    {
        get => StartingBalance;
        set => StartingBalance = value;
    }

    /// <summary>Optional. Defaults to admin when omitted.</summary>
    public string AdminUsername { get; set; } = "admin";

    /// <summary>Optional. Defaults to Admin123! when omitted.</summary>
    public string AdminPassword { get; set; } = string.Empty;
}
