using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;


namespace NickeltownFinance.Infrastructure.Repositories;

public class UserRepository : RepositoryBase<User>, IUserRepository
{
    public UserRepository(LiteDbContext context) : base(context, c => c.Users) { }

    public IEnumerable<User> GetActiveUsers() =>
        Collection.Find(x => x.IsActive).OrderBy(x => x.DisplayName);

    public User? GetByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var key = username.Trim().ToLowerInvariant();
        return Collection.FindOne(x => x.Username.ToLower() == key);
    }

    public User? GetByRole(UserRole role) =>
        Collection.FindOne(x => x.Role == role && x.IsActive);

    public int CountAdministrators(ObjectId? excludeUserId = null)
    {
        var admins = Collection.Find(x => x.Role == UserRole.Administrator && x.IsActive);
        if (excludeUserId is { } id)
            admins = admins.Where(x => x.Id != id);
        return admins.Count();
    }
}
