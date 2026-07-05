using LiteDB;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

public interface ISupplierProfileRepository : IRepository<SupplierProfile>
{
    SupplierProfile? FindByName(string name);

    SupplierProfile? FindByAbn(string abn);

    IEnumerable<SupplierProfile> SearchByAlias(string text);

    IEnumerable<SupplierProfile> SearchGlobal(string query);
}
