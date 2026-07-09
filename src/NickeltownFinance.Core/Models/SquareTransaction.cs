using LiteDB;

namespace NickeltownFinance.Core.Models;

/// <summary>
/// An individual Square sale/payment belonging to a <see cref="SquareDeposit"/>.
/// Stored as <c>square_transactions</c> (Square deposit line item).
/// </summary>
public class SquareTransaction : BaseEntity
{
    public ObjectId DepositId { get; set; } = ObjectId.Empty;

    public string ExternalDepositId { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    /// <summary>Date and time the payment was taken (local club time).</summary>
    public DateTime TransactionTime { get; set; }

    public DateTime? DepositDate { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PaymentId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal GrossAmount { get; set; }

    public decimal Fees { get; set; }

    public decimal NetAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string ExternalTransactionId { get; set; } = string.Empty;

    public string ImportFingerprint { get; set; } = string.Empty;

    public DateTime ImportedTimestamp { get; set; } = DateTime.UtcNow;

    public ObjectId? ImportBatchId { get; set; }

    public bool IsDeleted { get; set; }
}
