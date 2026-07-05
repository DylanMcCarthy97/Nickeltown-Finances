using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class SquareTransactionRepository : RepositoryBase<SquareTransaction>, ISquareTransactionRepository
{
    public SquareTransactionRepository(LiteDbContext context) : base(context, c => c.SquareTransactions) { }

    public IEnumerable<SquareTransaction> GetByDeposit(ObjectId depositId) =>
        Collection.Find(x => x.DepositId == depositId && !x.IsDeleted);

    public IEnumerable<SquareTransaction> GetByImportBatch(ObjectId batchId, bool includeDeleted = false) =>
        includeDeleted
            ? Collection.Find(x => x.ImportBatchId == batchId)
            : Collection.Find(x => x.ImportBatchId == batchId && !x.IsDeleted);

    public IEnumerable<SquareTransaction> FindByFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return [];

        return Collection.Find(x => x.ImportFingerprint == fingerprint && !x.IsDeleted);
    }

    public void InsertMany(IEnumerable<SquareTransaction> transactions)
    {
        var now = DateTime.UtcNow;
        var list = transactions.ToList();
        foreach (var txn in list)
        {
            txn.CreatedDate = now;
            txn.ModifiedDate = now;
        }

        if (list.Count > 0)
            Collection.InsertBulk(list);
    }
}
