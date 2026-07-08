using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services;

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _settingsRepository;

    public SettingsService(ISettingsRepository settingsRepository) =>
        _settingsRepository = settingsRepository;

    public string ClubName
    {
        get => _settingsRepository.GetValue(SettingKeys.ClubName) ?? "Nickeltown Flounderers Car Club";
        set => _settingsRepository.SetValue(SettingKeys.ClubName, value);
    }

    public string? ClubLogoPath
    {
        get => _settingsRepository.GetValue(SettingKeys.ClubLogoPath);
        set => _settingsRepository.SetValue(SettingKeys.ClubLogoPath, value ?? string.Empty);
    }

    public string? TreasurerSignaturePath
    {
        get
        {
            var path = _settingsRepository.GetValue(SettingKeys.TreasurerSignaturePath);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        set => _settingsRepository.SetValue(SettingKeys.TreasurerSignaturePath, value ?? string.Empty);
    }

    public int FinancialYearStartMonth
    {
        get => int.TryParse(_settingsRepository.GetValue(SettingKeys.FinancialYearStartMonth), out var m) ? m : 7;
        set => _settingsRepository.SetValue(SettingKeys.FinancialYearStartMonth, value.ToString());
    }

    public DateTime? TrackingStartDate
    {
        get => DateTime.TryParse(_settingsRepository.GetValue(SettingKeys.TrackingStartDate), out var d)
            ? d.Date
            : null;
        set => _settingsRepository.SetValue(
            SettingKeys.TrackingStartDate,
            value?.ToString("yyyy-MM-dd") ?? string.Empty);
    }

    public decimal? TrackingStartBalance
    {
        get => decimal.TryParse(_settingsRepository.GetValue(SettingKeys.TrackingStartBalance), out var b)
            ? b
            : null;
        set => _settingsRepository.SetValue(
            SettingKeys.TrackingStartBalance,
            value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
    }

    public decimal DefaultCashOnHand
    {
        get => decimal.TryParse(_settingsRepository.GetValue(SettingKeys.DefaultCashOnHand), out var v) ? v : 200m;
        set => _settingsRepository.SetValue(
            SettingKeys.DefaultCashOnHand,
            value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public decimal DefaultShireBonds
    {
        get => decimal.TryParse(_settingsRepository.GetValue(SettingKeys.DefaultShireBonds), out var v) ? v : 1000m;
        set => _settingsRepository.SetValue(
            SettingKeys.DefaultShireBonds,
            value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public decimal DefaultPayPalBalance
    {
        get => decimal.TryParse(_settingsRepository.GetValue(SettingKeys.DefaultPayPalBalance), out var v) ? v : 0m;
        set => _settingsRepository.SetValue(
            SettingKeys.DefaultPayPalBalance,
            value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public AppTheme Theme
    {
        get => Enum.TryParse<AppTheme>(_settingsRepository.GetValue(SettingKeys.Theme), out var t) ? t : AppTheme.System;
        set => _settingsRepository.SetValue(SettingKeys.Theme, value.ToString());
    }

    public string BackupFolder
    {
        get => _settingsRepository.GetValue(SettingKeys.BackupFolder)
               ?? Path.Combine(AppPaths.AppDataRoot, "backups");
        set => _settingsRepository.SetValue(SettingKeys.BackupFolder, value);
    }

    public string LastUsername
    {
        get => _settingsRepository.GetValue(SettingKeys.LastUsername) ?? string.Empty;
        set => _settingsRepository.SetValue(SettingKeys.LastUsername, value);
    }

    public bool RememberUsername
    {
        get => bool.TryParse(_settingsRepository.GetValue(SettingKeys.RememberUsername), out var r) && r;
        set => _settingsRepository.SetValue(SettingKeys.RememberUsername, value.ToString());
    }

    public double? WindowLeft
    {
        get => GetDouble(SettingKeys.WindowLeft);
        set => SetDouble(SettingKeys.WindowLeft, value);
    }

    public double? WindowTop
    {
        get => GetDouble(SettingKeys.WindowTop);
        set => SetDouble(SettingKeys.WindowTop, value);
    }

    public double? WindowWidth
    {
        get => GetDouble(SettingKeys.WindowWidth);
        set => SetDouble(SettingKeys.WindowWidth, value);
    }

    public double? WindowHeight
    {
        get => GetDouble(SettingKeys.WindowHeight);
        set => SetDouble(SettingKeys.WindowHeight, value);
    }

    public string? WindowState
    {
        get => _settingsRepository.GetValue(SettingKeys.WindowState);
        set => _settingsRepository.SetValue(SettingKeys.WindowState, value ?? string.Empty);
    }

    public bool SidebarCollapsed
    {
        get => bool.TryParse(_settingsRepository.GetValue(SettingKeys.SidebarCollapsed), out var c) && c;
        set => _settingsRepository.SetValue(SettingKeys.SidebarCollapsed, value.ToString());
    }

    public void Save() { }

    private double? GetDouble(string key) =>
        double.TryParse(_settingsRepository.GetValue(key), out var value) ? value : null;

    private void SetDouble(string key, double? value) =>
        _settingsRepository.SetValue(key, value?.ToString() ?? string.Empty);
}
