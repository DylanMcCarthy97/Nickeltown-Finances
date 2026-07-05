using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public sealed class SupplierProfileRepository : RepositoryBase<SupplierProfile>, ISupplierProfileRepository
{
    public SupplierProfileRepository(LiteDbContext context)
        : base(context, c => c.SupplierProfiles)
    {
    }

    public SupplierProfile? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();
        return Collection.FindOne(x => x.Name == normalized)
            ?? Collection.FindOne(x => x.Aliases.Any(a => a.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public SupplierProfile? FindByAbn(string abn)
    {
        if (string.IsNullOrWhiteSpace(abn)) return null;
        var digits = new string(abn.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        return Collection.FindOne(x => x.KnownAbns.Any(a =>
            new string(a.Where(char.IsDigit).ToArray()) == digits));
    }

    public IEnumerable<SupplierProfile> SearchByAlias(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var lower = text.ToLowerInvariant();
        return Collection.FindAll()
            .Where(p =>
                p.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || p.Aliases.Any(a => a.Contains(text, StringComparison.OrdinalIgnoreCase))
                || p.KnownAbns.Any(a => lower.Contains(a.ToLowerInvariant())));
    }

    public IEnumerable<SupplierProfile> SearchGlobal(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return GetAll();
        var q = query.Trim();
        var lower = q.ToLowerInvariant();
        return Collection.FindAll().Where(p =>
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || p.Aliases.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase))
            || p.KnownAbns.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase))
            || (p.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (p.Phone?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || (p.Website?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || p.KnownKeywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase))
            || (p.DefaultNotes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
    }
}
