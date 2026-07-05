using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategorisationRuleRepository _ruleRepository;

    public CategoryService(
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        ICategorisationRuleRepository ruleRepository)
    {
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
        _ruleRepository = ruleRepository;
    }

    public Task<IReadOnlyList<Category>> GetByTypeAsync(CategoryType type) =>
        Task.FromResult<IReadOnlyList<Category>>(
            _categoryRepository.GetByType(type)
                .Where(c => c.IsActive && !c.IsArchived)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList());

    public Task<IReadOnlyList<Category>> GetAllActiveAsync() =>
        Task.FromResult<IReadOnlyList<Category>>(
            _categoryRepository.GetActive()
                .Where(c => !c.IsArchived)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList());

    public Task<IReadOnlyList<Category>> GetAllAsync(bool includeArchived = false)
    {
        var items = _categoryRepository.GetAll()
            .Where(c => includeArchived || !c.IsArchived)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
        return Task.FromResult<IReadOnlyList<Category>>(items);
    }

    public Task SaveAsync(Category category)
    {
        var name = category.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Category name is required.");

        category.Name = name;

        var duplicate = _categoryRepository.GetAll()
            .FirstOrDefault(c => !c.IsArchived
                && c.Type == category.Type
                && c.Id != category.Id
                && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            throw new InvalidOperationException($"A {category.Type.ToString().ToLowerInvariant()} category named \"{name}\" already exists.");

        if (string.IsNullOrWhiteSpace(category.Icon))
            category.Icon = "Tag24";

        if (category.Id == ObjectId.Empty)
            _categoryRepository.Insert(category);
        else
            _categoryRepository.Update(category);
        return Task.CompletedTask;
    }

    public Task RenameAsync(ObjectId id, string newName)
    {
        var cat = _categoryRepository.GetById(id)
            ?? throw new InvalidOperationException("Category not found.");
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("Category name is required.");

        var trimmed = newName.Trim();
        var duplicate = _categoryRepository.GetAll()
            .FirstOrDefault(c => !c.IsArchived
                && c.Type == cat.Type
                && c.Id != id
                && c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
            throw new InvalidOperationException($"A category named \"{trimmed}\" already exists.");

        cat.Name = trimmed;
        _categoryRepository.Update(cat);
        return Task.CompletedTask;
    }

    public Task MergeAsync(ObjectId targetId, ObjectId sourceId)
    {
        if (targetId == sourceId)
            throw new InvalidOperationException("Choose two different categories to merge.");

        var target = _categoryRepository.GetById(targetId)
            ?? throw new InvalidOperationException("Target category not found.");
        var source = _categoryRepository.GetById(sourceId)
            ?? throw new InvalidOperationException("Source category not found.");

        if (target.Type != source.Type)
            throw new InvalidOperationException("Income and expense categories cannot be merged.");

        foreach (var txn in _transactionRepository.GetAll().Where(t => t.CategoryId == sourceId))
        {
            txn.CategoryId = targetId;
            _transactionRepository.Update(txn);
        }

        foreach (var rule in _ruleRepository.GetAllOrdered().Where(r => r.CategoryId == sourceId))
        {
            rule.CategoryId = targetId;
            _ruleRepository.Update(rule);
        }

        source.IsArchived = true;
        source.IsActive = false;
        _categoryRepository.Update(source);
        return Task.CompletedTask;
    }

    public Task ArchiveAsync(ObjectId id)
    {
        var cat = _categoryRepository.GetById(id);
        if (cat is null) return Task.CompletedTask;
        cat.IsArchived = true;
        cat.IsActive = false;
        _categoryRepository.Update(cat);
        return Task.CompletedTask;
    }

    public Task RestoreAsync(ObjectId id)
    {
        var cat = _categoryRepository.GetById(id);
        if (cat is null) return Task.CompletedTask;
        cat.IsArchived = false;
        cat.IsActive = true;
        _categoryRepository.Update(cat);
        return Task.CompletedTask;
    }

    public Task<bool> CanDeleteAsync(ObjectId id) =>
        Task.FromResult(_categoryRepository.GetUsageCount(id) == 0);

    public Task DeleteAsync(ObjectId id)
    {
        var cat = _categoryRepository.GetById(id);
        if (cat is null) return Task.CompletedTask;
        cat.IsActive = false;
        cat.IsArchived = true;
        _categoryRepository.Update(cat);
        return Task.CompletedTask;
    }
}
