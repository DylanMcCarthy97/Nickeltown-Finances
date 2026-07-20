using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class Transaction : BaseEntity
{
    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public ObjectId CategoryId { get; set; } = ObjectId.Empty;

    public decimal IncomeAmount { get; set; }

    public decimal ExpenseAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public ObjectId FinancialYearId { get; set; } = ObjectId.Empty;

    public ObjectId CreatedByUserId { get; set; } = ObjectId.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public ObjectId ModifiedByUserId { get; set; } = ObjectId.Empty;

    public string ModifiedByName { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ObjectId? ImportBatchId { get; set; }

    public decimal? StatementBalance { get; set; }

    public string ImportFingerprint { get; set; } = string.Empty;

    /// <summary>Linked Square deposit when this bank credit is a Square payout.</summary>
    public ObjectId? SquareDepositId { get; set; }

    /// <summary>True when description rules identify this as a Square deposit transfer.</summary>
    public bool IsSquareDeposit { get; set; }

    /// <summary>
    /// Optional category split for deposits that cover more than one income type
    /// (e.g. Bar Sales + Pitstop Sales). When present and totaling the transaction amount,
    /// reports use these lines instead of <see cref="CategoryId"/> alone.
    /// </summary>
    public List<CategoryAllocation> CategoryAllocations { get; set; } = [];
}

/// <summary>One category share of a split transaction.</summary>
public class CategoryAllocation
{
    public ObjectId CategoryId { get; set; } = ObjectId.Empty;

    public decimal Amount { get; set; }
}


