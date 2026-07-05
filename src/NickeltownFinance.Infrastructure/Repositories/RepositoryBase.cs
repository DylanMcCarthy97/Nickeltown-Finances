using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public abstract class RepositoryBase<T> : IRepository<T> where T : BaseEntity
{
    protected readonly LiteDbContext Context;
    protected readonly Func<LiteDbContext, ILiteCollection<T>> CollectionAccessor;

    protected RepositoryBase(LiteDbContext context, Func<LiteDbContext, ILiteCollection<T>> collectionAccessor)
    {
        Context = context;
        CollectionAccessor = collectionAccessor;
    }

    protected ILiteCollection<T> Collection => CollectionAccessor(Context);

    protected TResult RunLocked<TResult>(Func<TResult> action) => Context.ExecuteLocked(action);

    protected void RunLocked(Action action) => Context.ExecuteLocked(action);

    public T? GetById(ObjectId id) =>
        RunLocked(() => Collection.FindById(id));

    public IEnumerable<T> GetAll() =>
        RunLocked(() => Collection.FindAll().ToList());

    public void Insert(T entity) =>
        RunLocked(() =>
        {
            entity.CreatedDate = DateTime.UtcNow;
            entity.ModifiedDate = DateTime.UtcNow;
            Collection.Insert(entity);
        });

    public bool Update(T entity) =>
        RunLocked(() =>
        {
            entity.ModifiedDate = DateTime.UtcNow;
            return Collection.Update(entity);
        });

    public bool Delete(ObjectId id) =>
        RunLocked(() => Collection.Delete(id));
}
