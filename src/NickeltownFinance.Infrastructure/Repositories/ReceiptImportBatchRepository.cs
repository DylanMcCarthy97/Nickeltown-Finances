using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public sealed class ReceiptImportBatchRepository : RepositoryBase<ReceiptImportBatch>, IReceiptImportBatchRepository
{
    public ReceiptImportBatchRepository(LiteDbContext context)
        : base(context, c => c.ReceiptImportBatches)
    {
    }

    public IEnumerable<ReceiptImportBatch> GetHistory(int limit = 100) =>
        Collection.Query().OrderByDescending(x => x.ImportedAt).Limit(limit).ToList();
}
