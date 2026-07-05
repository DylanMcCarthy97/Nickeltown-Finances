using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Import;

namespace NickeltownFinance.Infrastructure.Services;

public class CategorisationService : ICategorisationService
{
    private readonly ICategorisationRuleRepository _ruleRepository;
    private readonly ICategoryRepository _categoryRepository;

    public CategorisationService(
        ICategorisationRuleRepository ruleRepository,
        ICategoryRepository categoryRepository)
    {
        _ruleRepository = ruleRepository;
        _categoryRepository = categoryRepository;
    }

    public Task<CategorisationSuggestion> SuggestDetailedAsync(string description, bool isIncome)
    {
        var rule = _ruleRepository.FindBestMatch(description);
        if (rule is null)
            return Task.FromResult(new CategorisationSuggestion());

        if (rule.Action == CategorisationRuleAction.SquareDeposit)
        {
            var squareCategory = FindCategory("Square Deposits", CategoryType.Income)
                                 ?? FindCategory("Bar Sales", CategoryType.Income);

            return Task.FromResult(new CategorisationSuggestion
            {
                CategoryId = squareCategory?.Id,
                CategoryName = squareCategory?.Name ?? "Square Deposit",
                IsSquareDeposit = true
            });
        }

        var category = _categoryRepository.GetById(rule.CategoryId);
        if (category is null || !category.IsActive)
            return Task.FromResult(new CategorisationSuggestion());

        var expectedType = isIncome ? CategoryType.Income : CategoryType.Expense;
        if (category.Type != expectedType)
            return Task.FromResult(new CategorisationSuggestion());

        return Task.FromResult(new CategorisationSuggestion
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            IsSquareDeposit = false
        });
    }

    public async Task<(ObjectId? CategoryId, string CategoryName)> SuggestAsync(string description, bool isIncome)
    {
        var suggestion = await SuggestDetailedAsync(description, isIncome);
        return (suggestion.CategoryId, suggestion.CategoryName);
    }

    public Task RememberAsync(string description, ObjectId categoryId)
    {
        var token = ExtractMatchToken(description);
        if (string.IsNullOrWhiteSpace(token) || categoryId == ObjectId.Empty)
            return Task.CompletedTask;

        var existing = _ruleRepository.FindByMatchText(token);
        if (existing is not null)
        {
            // Do not overwrite system Square-deposit rules with a category assignment.
            if (existing.Action == CategorisationRuleAction.SquareDeposit)
                return Task.CompletedTask;

            existing.CategoryId = categoryId;
            existing.Action = CategorisationRuleAction.AssignCategory;
            existing.HitCount++;
            existing.LastUsedUtc = DateTime.UtcNow;
            _ruleRepository.Update(existing);
        }
        else
        {
            _ruleRepository.Insert(new CategorisationRule
            {
                MatchText = token,
                CategoryId = categoryId,
                Action = CategorisationRuleAction.AssignCategory,
                Priority = 10,
                HitCount = 1,
                LastUsedUtc = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CategorisationRuleItem>> GetRulesAsync()
    {
        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var items = _ruleRepository.GetAllOrdered()
            .Select(r => new CategorisationRuleItem
            {
                Id = r.Id,
                MatchText = r.MatchText,
                CategoryId = r.CategoryId,
                CategoryName = r.Action == CategorisationRuleAction.SquareDeposit
                    ? "Square Deposit"
                    : categories.TryGetValue(r.CategoryId, out var c) ? c.Name : "Unknown",
                Action = r.Action,
                ActionDisplay = r.Action == CategorisationRuleAction.SquareDeposit ? "Square Deposit" : "Category",
                Priority = r.Priority,
                IsSystem = r.IsSystem,
                HitCount = r.HitCount,
                LastUsedUtc = r.LastUsedUtc
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<CategorisationRuleItem>>(items);
    }

    public Task SaveRuleAsync(CategorisationRule rule)
    {
        rule.MatchText = ImportFingerprint.Normalize(rule.MatchText);
        if (string.IsNullOrWhiteSpace(rule.MatchText))
            throw new InvalidOperationException("Match text is required.");

        if (rule.Id == ObjectId.Empty)
            _ruleRepository.Insert(rule);
        else
            _ruleRepository.Update(rule);

        return Task.CompletedTask;
    }

    public Task DeleteRuleAsync(ObjectId id)
    {
        _ruleRepository.Delete(id);
        return Task.CompletedTask;
    }

    public void EnsureDefaultRules()
    {
        EnsureCategory("Food", CategoryType.Expense, "#FB8C00");
        EnsureCategory("Rates", CategoryType.Expense, "#8E24AA");
        EnsureCategory("Square Deposits", CategoryType.Income, "#00897B");

        UpsertRule("BWS", "Bar Stock", CategoryType.Expense, priority: 50, isSystem: true);
        UpsertRule("WOOLWORTHS", "Food", CategoryType.Expense, priority: 50, isSystem: true);
        UpsertRule("COLES", "Food", CategoryType.Expense, priority: 50, isSystem: true);
        UpsertRule("SHIRE", "Rates", CategoryType.Expense, priority: 50, isSystem: true);
        UpsertRule("INSURANCE", "Insurance", CategoryType.Expense, priority: 50, isSystem: true);
        UpsertRule("TRANSFER FROM SQUARE", null, CategoryType.Income, priority: 100, isSystem: true,
            action: CategorisationRuleAction.SquareDeposit);
    }

    private void UpsertRule(
        string matchText,
        string? categoryName,
        CategoryType type,
        int priority,
        bool isSystem,
        CategorisationRuleAction action = CategorisationRuleAction.AssignCategory)
    {
        var existing = _ruleRepository.FindByMatchText(matchText);
        if (existing is not null)
            return;

        ObjectId categoryId = ObjectId.Empty;
        if (action == CategorisationRuleAction.AssignCategory && !string.IsNullOrWhiteSpace(categoryName))
        {
            var category = FindCategory(categoryName, type);
            if (category is null)
                return;
            categoryId = category.Id;
        }

        _ruleRepository.Insert(new CategorisationRule
        {
            MatchText = matchText.ToUpperInvariant(),
            CategoryId = categoryId,
            Action = action,
            Priority = priority,
            IsSystem = isSystem,
            HitCount = 0,
            LastUsedUtc = DateTime.UtcNow
        });
    }

    private void EnsureCategory(string name, CategoryType type, string colour)
    {
        if (FindCategory(name, type) is not null)
            return;

        var order = _categoryRepository.GetByType(type).Select(c => c.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        _categoryRepository.Insert(new Category
        {
            Name = name,
            Type = type,
            Colour = colour,
            SortOrder = order,
            IsActive = true
        });
    }

    private Category? FindCategory(string name, CategoryType type) =>
        _categoryRepository.GetByType(type)
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string ExtractMatchToken(string description)
    {
        var normalized = ImportFingerprint.Normalize(description);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ANZ", "EFTPOS", "VISA", "MASTERCARD", "PURCHASE", "PAYMENT", "TRANSFER", "TO", "FROM", "AUS",
            "CREDIT", "DEBIT", "PAID", "CARD", "POS", "AU", "NSW", "VIC", "QLD", "SA", "WA", "TAS", "ACT", "NT"
        };

        foreach (var part in parts)
        {
            if (part.Length < 3) continue;
            if (skip.Contains(part)) continue;
            if (part.All(char.IsDigit)) continue;
            return part;
        }

        return parts.FirstOrDefault(p => p.Length >= 3) ?? normalized;
    }
}
