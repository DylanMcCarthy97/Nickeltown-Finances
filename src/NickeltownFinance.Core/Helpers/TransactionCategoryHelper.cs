using LiteDB;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Helpers;

public static class TransactionCategoryHelper
{
    public static IEnumerable<(ObjectId CategoryId, decimal Amount)> GetIncomeCategoryAmounts(Transaction txn)
    {
        if (txn.IncomeAmount <= 0)
            yield break;

        var splits = txn.CategoryAllocations?
            .Where(a => a.CategoryId != ObjectId.Empty && a.Amount > 0)
            .ToList() ?? [];

        if (splits.Count >= 2)
        {
            var total = splits.Sum(a => a.Amount);
            if (Math.Abs(total - txn.IncomeAmount) <= 0.02m)
            {
                foreach (var split in splits)
                    yield return (split.CategoryId, split.Amount);
                yield break;
            }
        }

        yield return (txn.CategoryId, txn.IncomeAmount);
    }

    public static string FormatCategoryDisplay(
        Transaction txn,
        IReadOnlyDictionary<ObjectId, Category> categories)
    {
        var parts = GetIncomeCategoryAmounts(txn)
            .Select(a => categories.TryGetValue(a.CategoryId, out var c) ? c.Name : "Unknown")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count == 0)
            return categories.TryGetValue(txn.CategoryId, out var fallback) ? fallback.Name : "Unknown";

        return string.Join(" + ", parts);
    }
}