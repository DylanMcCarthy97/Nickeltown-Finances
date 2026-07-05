using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class FinancialYearRepository : RepositoryBase<FinancialYear>, IFinancialYearRepository
{
    public FinancialYearRepository(LiteDbContext context) : base(context, c => c.FinancialYears) { }

    public FinancialYear? GetCurrent() => Collection.FindOne(x => x.IsActive);

    public FinancialYear? GetByDate(DateTime date)
    {
        var day = date.Date;
        return Collection.FindOne(x => x.OpeningDate <= day && x.EndDate >= day);
    }

    public FinancialYear? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalised = name.Trim();
        return Collection.FindAll()
            .FirstOrDefault(x => x.Name.Equals(normalised, StringComparison.OrdinalIgnoreCase));
    }

    public bool Any() => Collection.Count() > 0;
}
