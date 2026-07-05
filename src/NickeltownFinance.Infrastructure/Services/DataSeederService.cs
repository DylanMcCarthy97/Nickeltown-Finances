using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.Infrastructure.Services;

public class DataSeederService : IDataSeederService
{
    private const string CurrentAuthVersion = "2";

    private readonly ISettingsRepository _settingsRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ISettingsService _settingsService;
    private readonly ICategorisationService _categorisationService;

    public DataSeederService(
        ISettingsRepository settingsRepository,
        IUserRepository userRepository,
        ICategoryRepository categoryRepository,
        IFinancialYearRepository financialYearRepository,
        ISettingsService settingsService,
        ICategorisationService categorisationService)
    {
        _settingsRepository = settingsRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _financialYearRepository = financialYearRepository;
        _settingsService = settingsService;
        _categorisationService = categorisationService;
    }

    public void SeedIfNeeded()
    {
        Directory.CreateDirectory(AppPaths.AppDataRoot);
        Directory.CreateDirectory(Path.Combine(AppPaths.AppDataRoot, "data"));
        Directory.CreateDirectory(_settingsService.BackupFolder);
        Directory.CreateDirectory(AppPaths.ExportsPath);
        Directory.CreateDirectory(AppPaths.LogsPath);

        // Existing installations keep their data; first-run setup creates FY + admin.
        if (_settingsRepository.GetValue(SettingKeys.IsInitialized) == "true")
        {
            MigrateAuthIfNeeded();
            _categorisationService.EnsureDefaultRules();
        }
    }

    public bool IsSetupRequired() => !_financialYearRepository.Any();

    public Task<(bool Success, string? Error)> CompleteFirstRunSetupAsync(FirstRunSetupRequest request)
    {
        if (_financialYearRepository.Any())
            return Task.FromResult<(bool, string?)>((false, "Setup has already been completed."));

        if (string.IsNullOrWhiteSpace(request.ClubName))
            return Task.FromResult<(bool, string?)>((false, "Club name is required."));

        if (request.StartingDate == default)
            return Task.FromResult<(bool, string?)>((false, "Starting date must be a valid date."));

        var startMonth = request.FinancialYearStartMonth is >= 1 and <= 12
            ? request.FinancialYearStartMonth
            : FinancialYearPeriod.DefaultStartMonth;

        var startingDate = request.StartingDate.Date;
        var startingBalance = request.StartingBalance;
        if (startingBalance is < -1_000_000_000m or > 1_000_000_000m)
            return Task.FromResult<(bool, string?)>((false, "Bank balance is out of range."));

        var (openingDate, endDate, name) = FinancialYearPeriod.ForDate(startingDate, startMonth);

        var adminUsername = string.IsNullOrWhiteSpace(request.AdminUsername)
            ? "admin"
            : request.AdminUsername.Trim();
        var adminPassword = string.IsNullOrWhiteSpace(request.AdminPassword)
            ? "Admin123!"
            : request.AdminPassword;

        var passwordError = PasswordRules.Validate(adminPassword);
        if (passwordError is not null)
            return Task.FromResult<(bool, string?)>((false, passwordError));

        SeedCategoriesIfEmpty();
        _categorisationService.EnsureDefaultRules();
        SeedDefaultSettings(request.ClubName.Trim(), startMonth, startingDate, startingBalance);

        _financialYearRepository.Insert(new FinancialYear
        {
            Name = name,
            OpeningDate = openingDate,
            EndDate = endDate,
            StartingDate = startingDate,
            StartingBalance = startingBalance,
            OpeningBalance = startingBalance,
            IsActive = true,
            IsArchived = false,
            IsLocked = false,
            Notes = "Created during first-run setup.",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        });

        foreach (var user in _userRepository.GetAll().ToList())
            _userRepository.Delete(user.Id);

        _userRepository.Insert(new User
        {
            Username = adminUsername,
            DisplayName = "Administrator",
            Role = UserRole.Administrator,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        });

        _settingsRepository.SetValue(SettingKeys.IsInitialized, "true");
        _settingsRepository.SetValue(SettingKeys.AuthVersion, CurrentAuthVersion);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    private void MigrateAuthIfNeeded()
    {
        var users = _userRepository.GetAll().ToList();
        var needsMigration =
            _settingsRepository.GetValue(SettingKeys.AuthVersion) != CurrentAuthVersion ||
            users.Count == 0 ||
            users.Any(u => string.IsNullOrWhiteSpace(u.Username) || string.IsNullOrWhiteSpace(u.PasswordHash));

        if (!needsMigration)
            return;

        // Only auto-reseed default users when no financial year exists yet (pre-setup).
        // After setup, an empty user list is an error state handled by the admin.
        if (!_financialYearRepository.Any())
            return;

        if (users.Count == 0)
            return;

        foreach (var user in users.Where(u => string.IsNullOrWhiteSpace(u.Username) || string.IsNullOrWhiteSpace(u.PasswordHash)).ToList())
            _userRepository.Delete(user.Id);

        if (_userRepository.GetAll().Any())
        {
            _settingsRepository.SetValue(SettingKeys.AuthVersion, CurrentAuthVersion);
            return;
        }

        // Legacy PIN-era wipe: restore a default admin only if every user was invalid.
        _userRepository.Insert(new User
        {
            Username = "admin",
            DisplayName = "Administrator",
            Role = UserRole.Administrator,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            IsActive = true
        });
        _settingsRepository.SetValue(SettingKeys.AuthVersion, CurrentAuthVersion);
    }

    private void SeedCategoriesIfEmpty()
    {
        if (_categoryRepository.GetAll().Any())
            return;

        var income = new[]
        {
            ("Membership Fees", "#2E7D32"),
            ("Bar Sales", "#388E3C"),
            ("Square Deposits", "#00897B"),
            ("Pitstop Sales", "#43A047"),
            ("Merchandise", "#4CAF50"),
            ("Donations", "#66BB6A"),
            ("Interest", "#81C784"),
            ("Other", "#A5D6A7")
        };

        var expense = new[]
        {
            ("Bar Stock", "#C62828"),
            ("Food", "#FB8C00"),
            ("Rates", "#8E24AA"),
            ("Equipment", "#D32F2F"),
            ("Insurance", "#E53935"),
            ("Utilities", "#EF5350"),
            ("Repairs", "#E57373"),
            ("Event Costs", "#EF9A9A"),
            ("Bank Fees", "#FFCDD2"),
            ("Miscellaneous", "#FFEBEE")
        };

        var order = 0;
        foreach (var (name, colour) in income)
        {
            _categoryRepository.Insert(new Category
            {
                Name = name,
                Type = CategoryType.Income,
                Colour = colour,
                SortOrder = order++
            });
        }

        order = 0;
        foreach (var (name, colour) in expense)
        {
            _categoryRepository.Insert(new Category
            {
                Name = name,
                Type = CategoryType.Expense,
                Colour = colour,
                SortOrder = order++
            });
        }
    }

    private void SeedDefaultSettings(
        string clubName,
        int financialYearStartMonth = FinancialYearPeriod.DefaultStartMonth,
        DateTime? trackingStartDate = null,
        decimal? trackingStartBalance = null)
    {
        _settingsService.ClubName = clubName;
        _settingsService.FinancialYearStartMonth = financialYearStartMonth;
        if (trackingStartDate is not null)
            _settingsService.TrackingStartDate = trackingStartDate.Value.Date;
        if (trackingStartBalance is not null)
            _settingsService.TrackingStartBalance = trackingStartBalance.Value;
        _settingsService.Theme = AppTheme.System;
        _settingsService.BackupFolder = Path.Combine(AppPaths.AppDataRoot, "backups");
        _settingsService.RememberUsername = true;
        _settingsService.Save();
    }
}
