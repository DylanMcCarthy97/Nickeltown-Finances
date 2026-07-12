using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class MonthDocumentRepository : RepositoryBase<MonthDocument>, IMonthDocumentRepository
{
    public MonthDocumentRepository(LiteDbContext context) : base(context, c => c.MonthDocuments) { }

    public IEnumerable<MonthDocument> GetForMonth(int year, int month, MonthDocumentKind? kind = null)
    {
        var query = Collection.Find(x => x.Year == year && x.Month == month);
        if (kind is not null)
            query = query.Where(x => x.Kind == kind.Value);

        return query.OrderBy(x => x.DateAdded).ThenBy(x => x.FileName);
    }
}