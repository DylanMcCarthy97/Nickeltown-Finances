using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class TransactionRepository : RepositoryBase<Transaction>, ITransactionRepository
{
    public TransactionRepository(LiteDbContext context) : base(context, c => c.Transactions) { }

    public IEnumerable<Transaction> GetByFinancialYear(ObjectId financialYearId, bool includeDeleted = false)
    {
        if (includeDeleted)
            return Collection.Find(x => x.FinancialYearId == financialYearId)
                .OrderBy(x => x.Date).ThenBy(x => x.CreatedDate);

        return Collection.Find(x => x.FinancialYearId == financialYearId && !x.IsDeleted)
            .OrderBy(x => x.Date).ThenBy(x => x.CreatedDate);
    }

    public IEnumerable<Transaction> GetByImportBatch(ObjectId batchId, bool includeDeleted = false)
    {
        if (includeDeleted)
            return Collection.Find(x => x.ImportBatchId == batchId)
                .OrderBy(x => x.Date).ThenBy(x => x.CreatedDate);

        return Collection.Find(x => x.ImportBatchId == batchId && !x.IsDeleted)
            .OrderBy(x => x.Date).ThenBy(x => x.CreatedDate);
    }

    public IEnumerable<Transaction> GetDeleted() =>
        Collection.Find(x => x.IsDeleted).OrderByDescending(x => x.DeletedAt);

    public IEnumerable<Transaction> GetByDateRange(DateTime start, DateTime end, ObjectId? financialYearFilter = null)
    {
        var endInclusive = end.Date.AddDays(1).AddTicks(-1);
        if (financialYearFilter is not null && financialYearFilter != ObjectId.Empty)
        {
            return Collection.Find(x =>
                !x.IsDeleted &&
                x.FinancialYearId == financialYearFilter &&
                x.Date >= start.Date &&
                x.Date <= endInclusive);
        }

        return Collection.Find(x => !x.IsDeleted && x.Date >= start.Date && x.Date <= endInclusive);
    }

    public IEnumerable<Transaction> FindByFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return [];

        return Collection.Find(x => !x.IsDeleted && x.ImportFingerprint == fingerprint);
    }

    public IEnumerable<Transaction> FindPotentialDuplicates(
        DateTime date,
        decimal amount,
        string description,
        string reference)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1).AddTicks(-1);
        var desc = description.Trim();
        var referenceTrimmed = reference.Trim();

        return Collection.Find(x => !x.IsDeleted && x.Date >= dayStart && x.Date <= dayEnd)
            .Where(x =>
            {
                var txnAmount = x.IncomeAmount > 0 ? x.IncomeAmount : x.ExpenseAmount;
                if (txnAmount != amount) return false;

                if (!string.IsNullOrEmpty(referenceTrimmed) &&
                    !string.Equals(x.Reference, referenceTrimmed, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(desc) &&
                    !string.Equals(x.Description, desc, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });
    }

    public void InsertMany(IEnumerable<Transaction> transactions)
    {
        var now = DateTime.UtcNow;
        var list = transactions.ToList();
        foreach (var entity in list)
        {
            entity.CreatedDate = now;
            entity.ModifiedDate = now;
        }

        if (list.Count > 0)
            Collection.InsertBulk(list);
    }
}
