using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services;

public class FinancialYearService : IFinancialYearService
{
    private readonly IFinancialYearRepository _repository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISettingsService _settingsService;
    private FinancialYear? _viewingYear;

    public event Action? ActiveYearChanged;
    public event Action? YearsChanged;

    public FinancialYearService(
        IFinancialYearRepository repository,
        ITransactionRepository transactionRepository,
        ISettingsService settingsService)
    {
        _repository = repository;
        _transactionRepository = transactionRepository;
        _settingsService = settingsService;
    }

    public FinancialYear? GetCurrent() => _repository.GetCurrent();

    public FinancialYear? GetById(ObjectId id) => _repository.GetById(id);

    public IReadOnlyList<FinancialYear> GetAll() =>
        _repository.GetAll().OrderByDescending(x => x.OpeningDate).ToList();

    public IReadOnlyList<FinancialYearListItem> GetListItems()
    {
        return GetAll().Select(fy =>
        {
            var hasTransactions = HasTransactions(fy.Id);
            var status = fy.IsLocked ? "Locked"
                : fy.IsActive ? "Active"
                : fy.IsArchived ? "Archived"
                : "Inactive";
            return new FinancialYearListItem
            {
                Id = fy.Id,
                Name = fy.Name,
                OpeningDate = fy.OpeningDate,
                StartingDate = EffectiveStartingDate(fy),
                StartingBalance = EffectiveStartingBalance(fy),
                OpeningBalance = fy.OpeningBalance,
                CurrentBalance = GetCurrentBalance(fy.Id),
                IsActive = fy.IsActive,
                IsArchived = fy.IsArchived,
                IsLocked = fy.IsLocked,
                Status = status,
                StatusKind = status,
                CanDelete = !hasTransactions,
                Notes = fy.Notes
            };
        }).ToList();
    }

    public FinancialYear GetActiveYear()
    {
        if (_viewingYear is not null)
        {
            var existing = _repository.GetById(_viewingYear.Id);
            if (existing is not null)
            {
                _viewingYear = existing;
                return existing;
            }
        }

        var year = _repository.GetCurrent()
                   ?? GetAll().FirstOrDefault(y => !y.IsArchived)
                   ?? GetAll().FirstOrDefault()
                   ?? throw new InvalidOperationException(
                       "No financial year is configured. Complete first-run setup.");

        _viewingYear = year;
        return year;
    }

    public void SetViewingYear(FinancialYear year)
    {
        ArgumentNullException.ThrowIfNull(year);

        var existing = _repository.GetById(year.Id) ?? year;
        if (_viewingYear?.Id == existing.Id)
            return;

        _viewingYear = existing;
        ActiveYearChanged?.Invoke();
    }

    public decimal GetCurrentBalance(ObjectId financialYearId)
    {
        var fy = _repository.GetById(financialYearId)
                 ?? throw new InvalidOperationException("Financial year not found.");

        decimal income = 0;
        decimal expenses = 0;
        foreach (var txn in _transactionRepository.GetByFinancialYear(financialYearId))
        {
            if (txn.IsDeleted) continue;
            income += txn.IncomeAmount;
            expenses += txn.ExpenseAmount;
        }

        return fy.OpeningBalance + income - expenses;
    }

    public decimal GetClosingBalance(ObjectId financialYearId) => GetCurrentBalance(financialYearId);

    public (DateTime StartingDate, decimal StartingBalance) GetTrackingStart()
    {
        if (_settingsService.TrackingStartDate is { } trackedDate &&
            _settingsService.TrackingStartBalance is { } trackedBalance)
            return (trackedDate.Date, trackedBalance);

        // Prefer the active year from first-run setup; fall back to earliest year.
        var fy = GetCurrent()
                 ?? GetAll().OrderBy(y => y.OpeningDate).FirstOrDefault()
                 ?? throw new InvalidOperationException("No financial year is configured.");

        EnsureStartingFields(fy);
        var start = EffectiveStartingDate(fy);
        var balance = EffectiveStartingBalance(fy);

        // Persist so later auto-created years do not steal the tracking start.
        _settingsService.TrackingStartDate = start;
        _settingsService.TrackingStartBalance = balance;
        _settingsService.Save();

        return (start, balance);
    }

    public decimal GetCurrentBankBalance()
    {
        var (startDate, startBalance) = GetTrackingStart();
        decimal net = 0;
        foreach (var txn in _transactionRepository.GetAll())
        {
            if (txn.IsDeleted) continue;
            // Starting balance is end-of-day on the starting date, so transactions
            // on that day (and earlier) are already included and must not be added again.
            if (txn.Date.Date <= startDate) continue;
            net += txn.IncomeAmount - txn.ExpenseAmount;
        }

        return startBalance + net;
    }

    public void RecalculateAllOpeningBalances()
    {
        foreach (var year in GetAll())
            RecalculateOpeningBalance(year.Id);
    }

    public bool HasTransactions(ObjectId financialYearId) =>
        _transactionRepository.GetByFinancialYear(financialYearId, includeDeleted: true).Any();

    public bool IsLocked(ObjectId financialYearId)
    {
        var fy = _repository.GetById(financialYearId);
        return fy?.IsLocked == true;
    }

    public FinancialYear EnsureYearForDate(DateTime date)
    {
        var existing = _repository.GetByDate(date);
        if (existing is not null)
            return existing;

        var startMonth = _settingsService.FinancialYearStartMonth;
        var (openingDate, endDate, name) = FinancialYearPeriod.ForDate(date, startMonth);

        var byName = _repository.GetByName(name);
        if (byName is not null)
            return byName;

        // Carry forward closing balance from the previous year when available.
        var previous = GetAll()
            .Where(y => y.EndDate < openingDate)
            .OrderByDescending(y => y.OpeningDate)
            .FirstOrDefault();

        var startingBalance = previous is not null ? GetClosingBalance(previous.Id) : 0m;

        var entity = new FinancialYear
        {
            Name = name,
            OpeningDate = openingDate,
            EndDate = endDate,
            StartingDate = openingDate,
            StartingBalance = startingBalance,
            OpeningBalance = startingBalance,
            IsActive = false,
            IsArchived = false,
            IsLocked = false,
            Notes = "Created automatically from transaction dates.",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _repository.Insert(entity);
        RecalculateAllOpeningBalances();
        RaiseChanged();
        return _repository.GetById(entity.Id) ?? entity;
    }

    public int EnsureYearsForDates(IEnumerable<DateTime> dates)
    {
        var countBefore = GetAll().Count;
        var startMonth = _settingsService.FinancialYearStartMonth;
        var openingDates = dates
            .Select(d => FinancialYearPeriod.ForDate(d.Date, startMonth).OpeningDate)
            .Distinct()
            .OrderBy(d => d);

        foreach (var openingDate in openingDates)
            EnsureYearForDate(openingDate.AddDays(7));

        return GetAll().Count - countBefore;
    }

    public bool TrySeedLegacyYearOpening(ObjectId financialYearId, DateTime startingDate, decimal openingBankBalance)
    {
        if (openingBankBalance == 0)
            return false;

        var fy = _repository.GetById(financialYearId);
        if (fy is null || fy.IsLocked || HasTransactions(financialYearId))
            return false;

        var day = startingDate.Date;
        if (day < fy.OpeningDate.Date || day > fy.EndDate.Date)
            return false;

        fy.StartingDate = day;
        fy.StartingBalance = openingBankBalance;
        fy.OpeningBalance = openingBankBalance;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        if (_viewingYear?.Id == fy.Id)
            _viewingYear = fy;

        return true;
    }

    public void RecalculateAfterLegacyImport(IEnumerable<DateTime> importedDates)
    {
        var (trackingDate, _) = GetTrackingStart();
        var affectedYearIds = importedDates
            .Select(d => EnsureYearForDate(d).Id)
            .Distinct()
            .ToList();

        foreach (var yearId in affectedYearIds)
        {
            var fy = _repository.GetById(yearId);
            if (fy is null || fy.OpeningDate.Date < trackingDate)
                continue;

            RecalculateOpeningBalance(yearId);
        }

        // Forward years may still need chaining even if they had no new transactions.
        foreach (var fy in GetAll().Where(y => y.OpeningDate.Date >= trackingDate))
            RecalculateOpeningBalance(fy.Id);
    }

    public Task<(bool Success, string? Error)> CreateNextYearAsync()
    {
        var current = GetCurrent()
                      ?? GetAll().Where(y => !y.IsArchived).OrderByDescending(y => y.OpeningDate).FirstOrDefault()
                      ?? GetAll().OrderByDescending(y => y.OpeningDate).FirstOrDefault();

        if (current is null)
            return Task.FromResult<(bool, string?)>((false, "No financial year exists yet. Complete first-run setup."));

        var openingDate = current.EndDate.Date.AddDays(1);
        var (_, endDate, name) = FinancialYearPeriod.ForDate(openingDate, _settingsService.FinancialYearStartMonth);

        if (_repository.GetByName(name) is not null || _repository.GetByDate(openingDate) is not null)
            return Task.FromResult<(bool, string?)>((false, $"Financial year {name} already exists."));

        var closingBalance = GetClosingBalance(current.Id);

        var entity = new FinancialYear
        {
            Name = name,
            OpeningDate = openingDate,
            EndDate = endDate,
            StartingDate = openingDate,
            StartingBalance = closingBalance,
            OpeningBalance = closingBalance,
            IsActive = true,
            IsArchived = false,
            IsLocked = false,
            Notes = $"Carried forward from {current.Name}.",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        // Archive and lock the previous year.
        current.IsActive = false;
        current.IsArchived = true;
        current.IsLocked = true;
        current.ModifiedDate = DateTime.UtcNow;
        _repository.Update(current);

        DeactivateAll();
        _repository.Insert(entity);
        RecalculateAllOpeningBalances();
        _viewingYear = _repository.GetById(entity.Id) ?? entity;
        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public void RecalculateOpeningBalance(ObjectId financialYearId)
    {
        var fy = _repository.GetById(financialYearId);
        if (fy is null) return;

        EnsureStartingFields(fy);

        var (trackingDate, trackingBalance) = GetTrackingStart();
        var yearStart = fy.OpeningDate.Date;

        // Opening balance at the start of this FY, derived from the club tracking start.
        // Tracking balance is end-of-day on the starting date (includes that day's activity).
        //   trackingBalance
        //   + net of transactions strictly after tracking date and before this FY starts
        //   − net of transactions in this FY on or before the tracking date
        decimal beforeYearPost = 0;
        decimal inYearPre = 0;

        foreach (var txn in _transactionRepository.GetAll())
        {
            if (txn.IsDeleted) continue;
            var day = txn.Date.Date;
            var net = txn.IncomeAmount - txn.ExpenseAmount;

            if (day > trackingDate && day < yearStart)
                beforeYearPost += net;
            else if (day >= yearStart && day <= trackingDate)
                inYearPre += net;
        }

        var opening = trackingBalance + beforeYearPost - inYearPre;
        if (fy.OpeningBalance == opening)
            return;

        fy.OpeningBalance = opening;

        // Keep the setup year's Starting* fields aligned with club tracking.
        if (yearStart <= trackingDate && trackingDate <= fy.EndDate.Date)
        {
            fy.StartingDate = trackingDate;
            fy.StartingBalance = trackingBalance;
        }

        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        if (_viewingYear?.Id == fy.Id)
            _viewingYear = fy;
    }

    public Task<(bool Success, string? Error)> CreateAsync(CreateFinancialYearRequest request)
    {
        var startMonth = _settingsService.FinancialYearStartMonth;
        var (openingDate, endDate, name) = FinancialYearPeriod.ForDate(
            request.OpeningDate.Date, startMonth);
        var startingDate = (request.StartingDate ?? openingDate).Date;
        var startingBalance = request.StartingBalance;

        var error = ValidateYearFields(name, openingDate, startingDate, startingBalance);
        if (error is not null)
            return Task.FromResult<(bool, string?)>((false, error));

        if (_repository.GetByName(name) is not null || _repository.GetByDate(openingDate) is not null)
            return Task.FromResult<(bool, string?)>((false, "A financial year for this period already exists."));

        if (request.CarryForwardPreviousClosingBalance)
        {
            var previous = GetAll()
                .Where(y => y.OpeningDate < openingDate)
                .OrderByDescending(y => y.OpeningDate)
                .FirstOrDefault();

            if (previous is null)
                return Task.FromResult<(bool, string?)>((false, "No previous financial year is available to carry forward."));

            startingBalance = GetClosingBalance(previous.Id);
            startingDate = openingDate;
        }

        var entity = new FinancialYear
        {
            Name = name,
            OpeningDate = openingDate,
            EndDate = endDate,
            StartingDate = startingDate,
            StartingBalance = startingBalance,
            OpeningBalance = startingBalance,
            Notes = request.Notes?.Trim() ?? string.Empty,
            IsActive = true,
            IsArchived = false,
            IsLocked = false,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        DeactivateAll();
        _repository.Insert(entity);
        RecalculateAllOpeningBalances();
        _viewingYear = _repository.GetById(entity.Id) ?? entity;
        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> UpdateStartingBalanceAsync(
        ObjectId id, DateTime startingDate, decimal startingBalance, string notes)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        if (fy.IsLocked)
            return Task.FromResult<(bool, string?)>((false, "This financial year is locked and cannot be edited."));

        var error = ValidateYearFields(fy.Name, fy.OpeningDate, startingDate, startingBalance);
        if (error is not null)
            return Task.FromResult<(bool, string?)>((false, error));

        fy.StartingDate = startingDate.Date;
        fy.StartingBalance = startingBalance;
        fy.Notes = notes?.Trim() ?? string.Empty;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        // Club-level tracking start — historical imports never overwrite this.
        _settingsService.TrackingStartDate = startingDate.Date;
        _settingsService.TrackingStartBalance = startingBalance;
        _settingsService.Save();

        RecalculateAllOpeningBalances();

        if (_viewingYear?.Id == fy.Id)
            _viewingYear = _repository.GetById(fy.Id) ?? fy;

        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> UpdateOpeningBalanceAsync(
        ObjectId id, DateTime openingDate, decimal openingBalance, string notes) =>
        UpdateStartingBalanceAsync(id, openingDate, openingBalance, notes);

    public Task<(bool Success, string? Error)> SetActiveAsync(ObjectId id)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        DeactivateAll();
        fy.IsActive = true;
        fy.IsArchived = false;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        _viewingYear = fy;
        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> ArchiveAsync(ObjectId id)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        if (fy.IsArchived)
            return Task.FromResult<(bool, string?)>((false, "This financial year is already archived."));

        var others = GetAll().Where(y => y.Id != id && !y.IsArchived).ToList();
        if (others.Count == 0)
            return Task.FromResult<(bool, string?)>((false, "Cannot archive the only financial year."));

        fy.IsActive = false;
        fy.IsArchived = true;
        fy.IsLocked = true;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        if (_repository.GetCurrent() is null)
        {
            var next = others.OrderByDescending(y => y.OpeningDate).First();
            next.IsActive = true;
            next.ModifiedDate = DateTime.UtcNow;
            _repository.Update(next);
            if (_viewingYear?.Id == fy.Id)
                _viewingYear = next;
        }
        else if (_viewingYear?.Id == fy.Id)
        {
            _viewingYear = _repository.GetCurrent() ?? others.First();
        }

        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> LockAsync(ObjectId id)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        if (fy.IsLocked)
            return Task.FromResult<(bool, string?)>((false, "This financial year is already locked."));

        fy.IsLocked = true;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        if (_viewingYear?.Id == fy.Id)
            _viewingYear = fy;

        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> UnlockAsync(ObjectId id)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        if (!fy.IsLocked)
            return Task.FromResult<(bool, string?)>((false, "This financial year is not locked."));

        fy.IsLocked = false;
        fy.ModifiedDate = DateTime.UtcNow;
        _repository.Update(fy);

        if (_viewingYear?.Id == fy.Id)
            _viewingYear = fy;

        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Success, string? Error)> DeleteAsync(ObjectId id)
    {
        var fy = _repository.GetById(id);
        if (fy is null)
            return Task.FromResult<(bool, string?)>((false, "Financial year not found."));

        if (HasTransactions(id))
            return Task.FromResult<(bool, string?)>((false, "Cannot delete a financial year that has transactions."));

        var remaining = GetAll().Where(y => y.Id != id).ToList();
        if (remaining.Count == 0)
            return Task.FromResult<(bool, string?)>((false, "Cannot delete the only financial year."));

        _repository.Delete(id);

        if (fy.IsActive)
        {
            var next = remaining.OrderByDescending(y => y.OpeningDate).First();
            next.IsActive = true;
            next.IsArchived = false;
            next.ModifiedDate = DateTime.UtcNow;
            _repository.Update(next);
            _viewingYear = next;
        }
        else if (_viewingYear?.Id == id)
        {
            _viewingYear = _repository.GetCurrent() ?? remaining.First();
        }

        RaiseChanged();
        return Task.FromResult<(bool, string?)>((true, null));
    }

    private void EnsureStartingFields(FinancialYear fy)
    {
        var changed = false;
        if (fy.StartingDate == default)
        {
            fy.StartingDate = fy.OpeningDate;
            changed = true;
        }

        // Legacy rows only had OpeningBalance; treat it as the user-entered starting balance
        // when StartingBalance was never set (migration leaves StartingBalance at 0 while OpeningBalance is set).
        if (fy.StartingBalance == 0 && fy.OpeningBalance != 0 && fy.StartingDate == fy.OpeningDate)
        {
            // Only apply when there are no pre-start transactions yet (OpeningBalance still equals starting).
            var hasPreStart = _transactionRepository.GetByFinancialYear(fy.Id)
                .Any(t => !t.IsDeleted && t.Date.Date <= fy.StartingDate.Date);
            if (!hasPreStart)
            {
                fy.StartingBalance = fy.OpeningBalance;
                changed = true;
            }
        }

        if (changed)
        {
            fy.ModifiedDate = DateTime.UtcNow;
            _repository.Update(fy);
        }
    }

    private static DateTime EffectiveStartingDate(FinancialYear fy) =>
        fy.StartingDate == default ? fy.OpeningDate : fy.StartingDate;

    private static decimal EffectiveStartingBalance(FinancialYear fy) =>
        fy.StartingDate == default && fy.StartingBalance == 0 ? fy.OpeningBalance : fy.StartingBalance;

    private void DeactivateAll()
    {
        foreach (var year in _repository.GetAll().Where(y => y.IsActive))
        {
            year.IsActive = false;
            year.ModifiedDate = DateTime.UtcNow;
            _repository.Update(year);
        }
    }

    private void RaiseChanged()
    {
        YearsChanged?.Invoke();
        ActiveYearChanged?.Invoke();
    }

    internal static string? ValidateYearFields(
        string name, DateTime openingDate, DateTime startingDate, decimal startingBalance)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Financial year is required.";

        if (openingDate == default)
            return "Financial year start date must be a valid date.";

        if (startingDate == default)
            return "Starting date must be a valid date.";

        if (startingBalance is < -1_000_000_000m or > 1_000_000_000m)
            return "Starting balance is out of range.";

        return null;
    }
}
