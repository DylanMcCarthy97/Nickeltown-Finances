using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(LiteDbContext context) : base(context, c => c.Categories) { }

    public IEnumerable<Category> GetByType(CategoryType type) =>
        Collection.Find(x => x.Type == type && x.IsActive).OrderBy(x => x.SortOrder);

    public IEnumerable<Category> GetActive() =>
        Collection.Find(x => x.IsActive).OrderBy(x => x.SortOrder);

    public int GetUsageCount(ObjectId categoryId) =>
        Context.Transactions.Count(x => x.CategoryId == categoryId && !x.IsDeleted);
}
