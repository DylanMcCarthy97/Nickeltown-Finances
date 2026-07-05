using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class ImportBatchRepository : RepositoryBase<ImportBatch>, IImportBatchRepository
{
    public ImportBatchRepository(LiteDbContext context) : base(context, c => c.ImportBatches) { }

    public IEnumerable<ImportBatch> GetHistory(int limit = 100) =>
        Collection.FindAll().OrderByDescending(x => x.ImportedAt).Take(limit);

    public ImportBatch? GetLatestBySource(ImportSourceType sourceType) =>
        Collection.Find(x => x.SourceType == sourceType && !x.IsUndone)
            .OrderByDescending(x => x.ImportedAt)
            .FirstOrDefault();
}
