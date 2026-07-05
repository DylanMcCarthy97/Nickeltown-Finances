using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class CategorisationRuleRepository : RepositoryBase<CategorisationRule>, ICategorisationRuleRepository
{
    public CategorisationRuleRepository(LiteDbContext context) : base(context, c => c.CategorisationRules) { }

    public CategorisationRule? FindBestMatch(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var upper = description.ToUpperInvariant();
        return Collection.FindAll()
            .Where(r => !string.IsNullOrWhiteSpace(r.MatchText) &&
                        upper.Contains(r.MatchText.ToUpperInvariant(), StringComparison.Ordinal))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.MatchText.Length)
            .ThenByDescending(r => r.HitCount)
            .FirstOrDefault();
    }

    public CategorisationRule? FindByMatchText(string matchText)
    {
        if (string.IsNullOrWhiteSpace(matchText))
            return null;

        var normalized = matchText.Trim().ToUpperInvariant();
        return Collection.FindAll()
            .FirstOrDefault(r => string.Equals(r.MatchText, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<CategorisationRule> GetAllOrdered() =>
        Collection.FindAll()
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.MatchText);
}
