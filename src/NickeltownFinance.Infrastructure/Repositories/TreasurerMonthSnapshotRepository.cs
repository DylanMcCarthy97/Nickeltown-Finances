using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class TreasurerMonthSnapshotRepository : RepositoryBase<TreasurerMonthSnapshot>, ITreasurerMonthSnapshotRepository
{
    public TreasurerMonthSnapshotRepository(LiteDbContext context)
        : base(context, c => c.TreasurerMonthSnapshots)
    {
    }

    public TreasurerMonthSnapshot? GetByYearMonth(int year, int month) =>
        RunLocked(() => Collection.FindOne(x => x.Year == year && x.Month == month));

    public void Upsert(TreasurerMonthSnapshot snapshot) =>
        RunLocked(() =>
        {
            var existing = Collection.FindOne(x => x.Year == snapshot.Year && x.Month == snapshot.Month);
            if (existing is null)
            {
                snapshot.CreatedDate = DateTime.UtcNow;
                snapshot.ModifiedDate = DateTime.UtcNow;
                Collection.Insert(snapshot);
                return;
            }

            snapshot.Id = existing.Id;
            snapshot.CreatedDate = existing.CreatedDate;
            snapshot.ModifiedDate = DateTime.UtcNow;
            Collection.Update(snapshot);
        });
}
