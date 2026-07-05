using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public sealed class ReceiptImportItemRepository : RepositoryBase<ReceiptImportItem>, IReceiptImportItemRepository
{
    public ReceiptImportItemRepository(LiteDbContext context)
        : base(context, c => c.ReceiptImportItems)
    {
    }

    public IEnumerable<ReceiptImportItem> GetInbox(bool includeCommitted = false) =>
        RunLocked(() =>
        {
            if (includeCommitted)
            {
                return Collection.Find(x => x.ImportTarget == ReceiptImportTarget.Inbox)
                    .OrderByDescending(x => x.CreatedDate)
                    .ToList();
            }

            return Collection.Find(x =>
                    x.ImportTarget == ReceiptImportTarget.Inbox
                    && x.Status != ReceiptImportStatus.Committed
                    && x.Status != ReceiptImportStatus.Ignored)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();
        });

    public IEnumerable<ReceiptImportItem> GetAllExceptIgnored() =>
        RunLocked(() => Collection.Find(x => x.Status != ReceiptImportStatus.Ignored).ToList());

    public IEnumerable<ReceiptImportItem> GetByUploadSessionKey(string uploadSessionKey) =>
        RunLocked(() =>
        {
            if (string.IsNullOrWhiteSpace(uploadSessionKey))
                return [];

            return Collection.Find(x => x.UploadSessionKey == uploadSessionKey)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();
        });

    public IEnumerable<ReceiptImportItem> GetByStatus(ReceiptImportStatus status) =>
        RunLocked(() => Collection.Find(x => x.Status == status).ToList());

    public ReceiptImportItem? FindByFileHash(string fileHash) =>
        RunLocked(() => Collection.FindOne(x => x.FileHash == fileHash));

    public IEnumerable<ReceiptImportItem> Search(string query, bool includeCommitted = false)
    {
        var q = query.Trim();
        if (string.IsNullOrWhiteSpace(q))
            return GetInbox(includeCommitted);

        var lower = q.ToLowerInvariant();
        return GetInbox(includeCommitted).Where(item =>
            (item.OcrSupplier?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.CorrectedSupplier?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.OcrInvoiceNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.CorrectedInvoiceNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.OcrAbn?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.CorrectedAbn?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.OcrFullText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.AiSuggestedCategoryName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.OcrTotal?.ToString("F2").Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.CorrectedTotal?.ToString("F2").Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || item.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || lower.Length >= 3 && (item.OcrFullText?.ToLowerInvariant().Contains(lower) ?? false));
    }
}
