using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class AttachmentRepository : RepositoryBase<Attachment>, IAttachmentRepository
{
    public AttachmentRepository(LiteDbContext context) : base(context, c => c.Attachments) { }

    public IEnumerable<Attachment> GetByTransaction(ObjectId transactionId) =>
        Collection.Find(x => x.TransactionId == transactionId).OrderBy(x => x.DateAdded);

    public IEnumerable<Attachment> GetByTransactions(IEnumerable<ObjectId> transactionIds)
    {
        var ids = transactionIds.ToHashSet();
        if (ids.Count == 0) return [];
        return Collection.FindAll().Where(x => ids.Contains(x.TransactionId));
    }

    public IEnumerable<Attachment> SearchByFileName(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];
        var s = search.Trim().ToLowerInvariant();
        return Collection.FindAll().Where(x => x.FileName.ToLowerInvariant().Contains(s));
    }

    public IEnumerable<Attachment> SearchByOcrText(string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];
        var s = search.Trim();
        return Collection.FindAll().Where(a =>
            (a.OcrSupplier?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || (a.OcrAbn?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || (a.OcrInvoiceNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || (a.OcrPaymentMethod?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || a.FileName.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    public HashSet<ObjectId> GetTransactionIdsWithAttachments(AttachmentKind? kind = null)
    {
        IEnumerable<Attachment> query = Collection.FindAll();
        if (kind is not null)
            query = query.Where(x => x.Kind == kind.Value);

        return query.Select(x => x.TransactionId).ToHashSet();
    }
}
