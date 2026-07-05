using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptDuplicateDetector : IReceiptDuplicateDetector
{
    private readonly IReceiptImportItemRepository _items;

    public ReceiptDuplicateDetector(IReceiptImportItemRepository items) => _items = items;

    public Task<ReceiptDuplicateCheckResult> CheckAsync(
        ReceiptImportItem item,
        CancellationToken cancellationToken = default)
    {
        var others = _items.GetAllExceptIgnored()
            .Where(x => x.Id != item.Id)
            .ToList();

        var hashMatch = others.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.FileHash)
            && x.FileHash == item.FileHash
            && x.Status is not ReceiptImportStatus.Ignored);

        if (hashMatch is not null)
        {
            return Task.FromResult(new ReceiptDuplicateCheckResult
            {
                IsPossibleDuplicate = true,
                ExistingItemId = hashMatch.Id,
                Warning = "Possible duplicate receipt (identical file)."
            });
        }

        var supplier = item.CorrectedSupplier ?? item.OcrSupplier;
        var total = item.CorrectedTotal ?? item.OcrTotal;
        var date = item.CorrectedDate ?? item.OcrDate;
        if (string.IsNullOrWhiteSpace(supplier) || !total.HasValue || !date.HasValue)
            return Task.FromResult(new ReceiptDuplicateCheckResult());

        var fuzzy = others.FirstOrDefault(x =>
        {
            var otherSupplier = x.CorrectedSupplier ?? x.OcrSupplier;
            var otherTotal = x.CorrectedTotal ?? x.OcrTotal;
            var otherDate = x.CorrectedDate ?? x.OcrDate;
            if (string.IsNullOrWhiteSpace(otherSupplier) || !otherTotal.HasValue || !otherDate.HasValue)
                return false;
            if (Math.Abs(otherTotal.Value - total.Value) > 0.01m) return false;
            if (Math.Abs((otherDate.Value.Date - date.Value.Date).Days) > 1) return false;
            return otherSupplier.Contains(supplier, StringComparison.OrdinalIgnoreCase)
                || supplier.Contains(otherSupplier, StringComparison.OrdinalIgnoreCase);
        });

        if (fuzzy is null)
            return Task.FromResult(new ReceiptDuplicateCheckResult());

        return Task.FromResult(new ReceiptDuplicateCheckResult
        {
            IsPossibleDuplicate = true,
            ExistingItemId = fuzzy.Id,
            Warning = "Possible duplicate receipt (same supplier, amount, and date)."
        });
    }
}
