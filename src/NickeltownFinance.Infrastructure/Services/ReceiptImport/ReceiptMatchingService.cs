using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptMatchingService : IReceiptMatchingService
{
    private const int MatchWindowDays = 7;
    private const decimal AmountTolerance = 0.01m;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IReceiptImportItemRepository _itemRepository;

    public ReceiptMatchingService(
        ITransactionRepository transactionRepository,
        IReceiptImportItemRepository itemRepository)
    {
        _transactionRepository = transactionRepository;
        _itemRepository = itemRepository;
    }

    public Task<ReceiptMatchSuggestionInfo?> FindBestMatchAsync(
        ReceiptImportItem item,
        CancellationToken cancellationToken = default)
    {
        var matches = FindMatchesInternal(item);
        return Task.FromResult(matches.FirstOrDefault());
    }

    public Task<IReadOnlyList<ReceiptMatchSuggestionInfo>> FindMatchesAsync(
        ObjectId importItemId,
        CancellationToken cancellationToken = default)
    {
        var item = _itemRepository.GetById(importItemId);
        if (item is null)
            return Task.FromResult<IReadOnlyList<ReceiptMatchSuggestionInfo>>(Array.Empty<ReceiptMatchSuggestionInfo>());

        return Task.FromResult<IReadOnlyList<ReceiptMatchSuggestionInfo>>(FindMatchesInternal(item));
    }

    private List<ReceiptMatchSuggestionInfo> FindMatchesInternal(ReceiptImportItem item)
    {
        var targetAmount = item.CorrectedTotal ?? item.OcrTotal;
        if (!targetAmount.HasValue)
            return [];

        var targetDate = (item.CorrectedDate ?? item.OcrDate)?.Date ?? DateTime.Today;
        var supplier = item.CorrectedSupplier ?? item.OcrSupplier ?? item.DetectedSupplierName;
        var from = targetDate.AddDays(-MatchWindowDays);
        var to = targetDate.AddDays(MatchWindowDays);

        return _transactionRepository
            .GetByDateRange(from, to)
            .Where(t => !t.IsDeleted && t.ExpenseAmount > 0)
            .Select(t => new
            {
                Transaction = t,
                Score = ScoreMatch(t, targetAmount.Value, targetDate, supplier)
            })
            .Where(x => x.Score >= 50)
            .OrderByDescending(x => x.Score)
            .Take(10)
            .Select(x => new ReceiptMatchSuggestionInfo
            {
                TransactionId = x.Transaction.Id,
                Description = x.Transaction.Description,
                Date = x.Transaction.Date,
                Amount = x.Transaction.ExpenseAmount,
                Confidence = (byte)Math.Min(100, x.Score),
                Quality = ToQuality(x.Score)
            })
            .ToList();
    }

    internal static ReceiptMatchQuality ToQuality(int score) => score switch
    {
        >= 90 => ReceiptMatchQuality.Excellent,
        >= 75 => ReceiptMatchQuality.Likely,
        >= 50 => ReceiptMatchQuality.Possible,
        _ => ReceiptMatchQuality.None
    };

    private static int ScoreMatch(
        Transaction transaction,
        decimal targetAmount,
        DateTime targetDate,
        string? supplier)
    {
        var score = 0;

        if (Math.Abs(transaction.ExpenseAmount - targetAmount) <= AmountTolerance)
            score += 45;
        else if (Math.Abs(transaction.ExpenseAmount - targetAmount) <= 1m)
            score += 25;
        else
            return 0;

        var dayDiff = Math.Abs((transaction.Date.Date - targetDate).Days);
        score += Math.Max(0, 25 - dayDiff * 4);

        if (!string.IsNullOrWhiteSpace(supplier))
        {
            if (transaction.Description.Contains(supplier, StringComparison.OrdinalIgnoreCase))
                score += 20;
            else if (FuzzyContains(transaction.Description, supplier))
                score += 10;
        }

        if (transaction.Reference.Contains(supplier ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            score += 5;

        return Math.Min(100, score);
    }

    private static bool FuzzyContains(string haystack, string needle)
    {
        var tokens = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 0 && tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase)) >= Math.Max(1, tokens.Length / 2);
    }
}
