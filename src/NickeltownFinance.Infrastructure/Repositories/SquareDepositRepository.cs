using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class SquareDepositRepository : RepositoryBase<SquareDeposit>, ISquareDepositRepository
{
    public SquareDepositRepository(LiteDbContext context) : base(context, c => c.SquareDeposits) { }

    public SquareDeposit? GetByExternalDepositId(string depositId)
    {
        if (string.IsNullOrWhiteSpace(depositId))
            return null;

        return Collection.FindOne(x => x.DepositId == depositId && !x.IsDeleted);
    }

    public IEnumerable<SquareDeposit> GetActive() =>
        Collection.Find(x => !x.IsDeleted);

    public IEnumerable<SquareDeposit> GetByStatus(SquareDepositStatus status) =>
        Collection.Find(x => !x.IsDeleted && x.Status == status);

    public IEnumerable<SquareDeposit> GetUnmatched() =>
        Collection.Find(x => !x.IsDeleted &&
                             (x.Status == SquareDepositStatus.WaitingForMatch ||
                              x.Status == SquareDepositStatus.ManualReview));

    public IEnumerable<SquareDeposit> GetByImportBatch(ObjectId batchId, bool includeDeleted = false) =>
        includeDeleted
            ? Collection.Find(x => x.ImportBatchId == batchId)
            : Collection.Find(x => x.ImportBatchId == batchId && !x.IsDeleted);

    public IEnumerable<SquareDeposit> FindByNetAmount(decimal netAmount) =>
        Collection.Find(x => !x.IsDeleted && x.NetAmount == netAmount);

    public void InsertMany(IEnumerable<SquareDeposit> deposits)
    {
        var now = DateTime.UtcNow;
        var list = deposits.ToList();
        foreach (var deposit in list)
        {
            deposit.CreatedDate = now;
            deposit.ModifiedDate = now;
        }

        if (list.Count > 0)
            Collection.InsertBulk(list);
    }
}
