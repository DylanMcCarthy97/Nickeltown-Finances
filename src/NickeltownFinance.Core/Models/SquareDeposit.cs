using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

/// <summary>
/// A Square payout/deposit awaiting (or linked to) an ANZ bank transfer.
/// Square data is never written directly into the Transactions ledger.
/// </summary>
public class SquareDeposit : BaseEntity
{
    public string DepositId { get; set; } = string.Empty;

    public DateTime DepositDate { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal Fees { get; set; }

    public decimal NetAmount { get; set; }

    public int TransactionCount { get; set; }

    public SquareDepositStatus Status { get; set; } = SquareDepositStatus.WaitingForMatch;

    public ObjectId? MatchedTransactionId { get; set; }

    public ObjectId? ImportBatchId { get; set; }

    public string ImportFingerprint { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}
