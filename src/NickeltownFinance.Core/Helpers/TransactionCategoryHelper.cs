using LiteDB;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Helpers;

public static class TransactionCategoryHelper
{
    public static IEnumerable<ObjectId> GetAllCategoryIds(Transaction txn)
    {
        if (txn.CategoryId != ObjectId.Empty)
            yield return txn.CategoryId;

        foreach (var id in GetExtraCategoryIds(txn))
            yield return id;
    }

    public static IEnumerable<ObjectId> GetExtraCategoryIds(Transaction txn)
    {
        var seen = new HashSet<ObjectId> { txn.CategoryId };
        foreach (var allocation in txn.CategoryAllocations ?? [])
        {
            if (allocation.CategoryId == ObjectId.Empty || !seen.Add(allocation.CategoryId))
                continue;
            yield return allocation.CategoryId;
        }
    }

    public static string FormatCategoryDisplay(
        Transaction txn,
        IReadOnlyDictionary<ObjectId, Category> categories)
    {
        var parts = GetAllCategoryIds(txn)
            .Select(id => categories.TryGetValue(id, out var c) ? c.Name : "Unknown")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count == 0)
            return "Unknown";

        return string.Join(" + ", parts);
    }

    public static bool MatchesCategoryFilter(Transaction txn, ObjectId categoryId) =>
        categoryId != ObjectId.Empty && GetAllCategoryIds(txn).Any(id => id == categoryId);
}
