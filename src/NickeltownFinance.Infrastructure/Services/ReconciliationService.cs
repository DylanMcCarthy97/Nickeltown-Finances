using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services;

public class ReconciliationService : IReconciliationService
{
    private const decimal AmountTolerance = 0.01m;
    private const int DateToleranceDays = 2;

    private static readonly string[] SquareTransferMarkers =
    [
        "TRANSFER FROM SQUARE",
        "SQUARE INC",
        "SQUARE AUSTRALIA"
    ];

    private readonly ITransactionRepository _transactionRepository;
    private readonly ISquareDepositRepository _depositRepository;
    private readonly IImportBatchRepository _importBatchRepository;

    public ReconciliationService(
        ITransactionRepository transactionRepository,
        ISquareDepositRepository depositRepository,
        IImportBatchRepository importBatchRepository)
    {
        _transactionRepository = transactionRepository;
        _depositRepository = depositRepository;
        _importBatchRepository = importBatchRepository;
    }

    public Task<ReconciliationSummary> GetSummaryAsync()
    {
        var squareTransfers = GetSquareBankTransfers().ToList();
        var matchedTxns = squareTransfers.Where(t => t.SquareDepositId is not null).ToList();
        var unmatchedAnz = squareTransfers.Where(t => t.SquareDepositId is null).ToList();
        var unmatchedSquare = _depositRepository.GetUnmatched().ToList();
        var manualReview = unmatchedSquare.Where(d => d.Status == SquareDepositStatus.ManualReview).ToList();

        var matchedItems = new List<ReconciliationMatchItem>();
        foreach (var txn in matchedTxns)
        {
            var deposit = txn.SquareDepositId is { } id ? _depositRepository.GetById(id) : null;
            if (deposit is null) continue;
            matchedItems.Add(new ReconciliationMatchItem
            {
                TransactionId = txn.Id,
                SquareDepositId = deposit.Id,
                BankDate = txn.Date,
                DepositDate = deposit.DepositDate,
                Description = txn.Description,
                DepositId = deposit.DepositId,
                Amount = txn.IncomeAmount
            });
        }

        var reviewCandidates = new List<ReconciliationMatchCandidate>();
        foreach (var txn in unmatchedAnz)
        {
            var candidates = FindCandidates(txn).ToList();
            if (candidates.Count > 1)
            {
                reviewCandidates.Add(new ReconciliationMatchCandidate
                {
                    AnzDeposit = ToAnzItem(txn),
                    PossibleSquareDeposits = candidates.Select(ToSquareItem).ToList()
                });
            }
        }

        // Also surface deposits already marked ManualReview.
        foreach (var deposit in manualReview)
        {
            if (reviewCandidates.Any(c => c.PossibleSquareDeposits.Any(p => p.Id == deposit.Id)))
                continue;

            var amountMatches = unmatchedAnz
                .Where(t => AmountsMatch(t.IncomeAmount, deposit.NetAmount))
                .Select(ToAnzItem)
                .ToList();

            if (amountMatches.Count == 0)
                continue;

            foreach (var anz in amountMatches)
            {
                var existing = reviewCandidates.FirstOrDefault(c => c.AnzDeposit.TransactionId == anz.TransactionId);
                if (existing is not null)
                {
                    if (existing.PossibleSquareDeposits.All(p => p.Id != deposit.Id))
                    {
                        existing.PossibleSquareDeposits =
                            existing.PossibleSquareDeposits.Append(ToSquareItem(deposit)).ToList();
                    }
                }
                else
                {
                    reviewCandidates.Add(new ReconciliationMatchCandidate
                    {
                        AnzDeposit = anz,
                        PossibleSquareDeposits = [ToSquareItem(deposit)]
                    });
                }
            }
        }

        var recentErrors = _importBatchRepository.GetHistory(20).Sum(b => b.Errors);
        var duplicates = _importBatchRepository.GetHistory(20).Sum(b => b.DuplicatesDetected);

        return Task.FromResult(new ReconciliationSummary
        {
            BankDeposits = squareTransfers.Count,
            Matched = matchedItems.Count,
            NeedsReview = reviewCandidates.Count,
            UnmatchedAnz = unmatchedAnz.Count,
            UnmatchedSquare = unmatchedSquare.Count,
            DuplicateTransactions = duplicates,
            ImportErrors = recentErrors,
            ReconciledTotal = matchedItems.Sum(m => m.Amount),
            MatchedDeposits = matchedItems.OrderByDescending(m => m.BankDate).ToList(),
            UnmatchedAnzDeposits = unmatchedAnz.Select(ToAnzItem).OrderByDescending(x => x.Date).ToList(),
            UnmatchedSquareDeposits = unmatchedSquare.Select(ToSquareItem).OrderByDescending(x => x.DepositDate).ToList(),
            ManualReviewItems = reviewCandidates
        });
    }

    public Task<(int Matched, int NeedsReview)> MatchTransactionsAsync(IReadOnlyList<ObjectId> transactionIds)
    {
        var matched = 0;
        var needsReview = 0;

        foreach (var id in transactionIds)
        {
            var txn = _transactionRepository.GetById(id);
            if (txn is null || txn.IsDeleted)
                continue;

            if (!txn.IsSquareDeposit && !IsSquareTransferDescription(txn.Description))
                continue;

            if (txn.SquareDepositId is not null)
                continue;

            txn.IsSquareDeposit = true;
            var candidates = FindCandidates(txn).ToList();

            if (candidates.Count == 1)
            {
                Link(txn, candidates[0]);
                matched++;
            }
            else if (candidates.Count > 1)
            {
                foreach (var candidate in candidates)
                {
                    candidate.Status = SquareDepositStatus.ManualReview;
                    candidate.ModifiedDate = DateTime.UtcNow;
                    _depositRepository.Update(candidate);
                }

                txn.ModifiedDate = DateTime.UtcNow;
                _transactionRepository.Update(txn);
                needsReview++;
            }
            else
            {
                txn.ModifiedDate = DateTime.UtcNow;
                _transactionRepository.Update(txn);
            }
        }

        return Task.FromResult((matched, needsReview));
    }

    public Task<(bool Success, string? Error)> ManualMatchAsync(ObjectId transactionId, ObjectId squareDepositId)
    {
        var txn = _transactionRepository.GetById(transactionId);
        if (txn is null || txn.IsDeleted)
            return Task.FromResult<(bool, string?)>((false, "Bank transaction not found."));

        var deposit = _depositRepository.GetById(squareDepositId);
        if (deposit is null || deposit.IsDeleted)
            return Task.FromResult<(bool, string?)>((false, "Square deposit not found."));

        if ((deposit.Status is SquareDepositStatus.Matched or SquareDepositStatus.Imported) &&
            deposit.MatchedTransactionId is not null &&
            deposit.MatchedTransactionId != transactionId)
            return Task.FromResult<(bool, string?)>((false, "That Square deposit is already matched."));

        if (txn.SquareDepositId is not null && txn.SquareDepositId != squareDepositId)
            return Task.FromResult<(bool, string?)>((false, "That bank deposit is already matched."));

        Link(txn, deposit);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> UnmatchAsync(ObjectId transactionId)
    {
        var txn = _transactionRepository.GetById(transactionId);
        if (txn is null)
            return Task.FromResult<(bool, string?)>((false, "Bank transaction not found."));

        if (txn.SquareDepositId is not { } depositId)
            return Task.FromResult<(bool, string?)>((false, "Transaction is not matched."));

        var deposit = _depositRepository.GetById(depositId);
        if (deposit is not null)
        {
            deposit.Status = SquareDepositStatus.WaitingForMatch;
            deposit.MatchedTransactionId = null;
            deposit.ModifiedDate = DateTime.UtcNow;
            _depositRepository.Update(deposit);
        }

        txn.SquareDepositId = null;
        txn.ModifiedDate = DateTime.UtcNow;
        _transactionRepository.Update(txn);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    private IEnumerable<SquareDeposit> FindCandidates(Transaction txn)
    {
        var amount = txn.IncomeAmount > 0 ? txn.IncomeAmount : txn.ExpenseAmount;
        var unmatched = _depositRepository.GetUnmatched()
            .Where(d => AmountsMatch(d.NetAmount, amount))
            .ToList();

        // Prefer deposit ID present in the bank description/reference.
        var description = txn.Description ?? string.Empty;
        var reference = txn.Reference ?? string.Empty;
        var withId = unmatched.Where(d =>
            !string.IsNullOrWhiteSpace(d.DepositId) &&
            (description.Contains(d.DepositId, StringComparison.OrdinalIgnoreCase) ||
             reference.Contains(d.DepositId, StringComparison.OrdinalIgnoreCase))).ToList();
        if (withId.Count == 1)
            return withId;

        // Prefer deposit date within tolerance of the bank credit.
        var byDate = unmatched
            .Where(d => Math.Abs((d.DepositDate.Date - txn.Date.Date).TotalDays) <= DateToleranceDays)
            .ToList();
        if (byDate.Count == 1)
            return byDate;

        if (byDate.Count > 1)
            return byDate;

        return unmatched;
    }

    private static bool AmountsMatch(decimal a, decimal b) => Math.Abs(a - b) <= AmountTolerance;

    private void Link(Transaction txn, SquareDeposit deposit)
    {
        var now = DateTime.UtcNow;
        deposit.Status = SquareDepositStatus.Matched;
        deposit.MatchedTransactionId = txn.Id;
        deposit.ModifiedDate = now;
        _depositRepository.Update(deposit);

        txn.IsSquareDeposit = true;
        txn.SquareDepositId = deposit.Id;
        txn.ModifiedDate = now;
        _transactionRepository.Update(txn);
    }

    private IEnumerable<Transaction> GetSquareBankTransfers() =>
        _transactionRepository.GetAll()
            .Where(t => !t.IsDeleted &&
                        (t.IsSquareDeposit || IsSquareTransferDescription(t.Description)) &&
                        t.IncomeAmount > 0);

    public static bool IsSquareTransferDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var upper = description.ToUpperInvariant();
        return SquareTransferMarkers.Any(m => upper.Contains(m, StringComparison.Ordinal));
    }

    private static ReconciliationAnzItem ToAnzItem(Transaction txn) => new()
    {
        TransactionId = txn.Id,
        Date = txn.Date,
        Description = txn.Description ?? string.Empty,
        Amount = txn.IncomeAmount,
        Reference = txn.Reference ?? string.Empty
    };

    private static ReconciliationSquareItem ToSquareItem(SquareDeposit deposit) => new()
    {
        Id = deposit.Id,
        DepositId = deposit.DepositId,
        DepositDate = deposit.DepositDate,
        NetAmount = deposit.NetAmount,
        GrossAmount = deposit.GrossAmount,
        Fees = deposit.Fees,
        TransactionCount = deposit.TransactionCount,
        Status = deposit.Status switch
        {
            SquareDepositStatus.WaitingForMatch => "Waiting for Match",
            SquareDepositStatus.Matched => "Matched",
            SquareDepositStatus.ManualReview => "Manual Review",
            SquareDepositStatus.Imported => "Imported",
            _ => deposit.Status.ToString()
        }
    };
}
