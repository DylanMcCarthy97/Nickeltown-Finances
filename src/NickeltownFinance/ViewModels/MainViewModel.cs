using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core;
using NickeltownFinance.Core.Update;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static bool _startupUpdateChecked;
    private bool _detached;

    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionService _sessionService;
    private readonly IBackupService _backupService;
    private readonly INotificationService _notificationService;
    private readonly IFinancialYearService _financialYearService;
    private readonly ISettingsService _settingsService;
    private readonly IAppUpdateService _appUpdateService;
    private bool _suppressYearChange;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _windowTitle = "Nickeltown Finance";
    [ObservableProperty] private string _userDisplayName = string.Empty;
    [ObservableProperty] private string _userRoleDisplay = string.Empty;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _financialYearStatus = string.Empty;
    [ObservableProperty] private string _currentPageName = "Dashboard";
    [ObservableProperty] private string _activeNavKey = "Dashboard";
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private bool _isAdministrator;
    [ObservableProperty] private GridLength _sidebarWidth = new(260);
    [ObservableProperty] private string _clubName = string.Empty;
    [ObservableProperty] private ImageSource _clubLogoImage = null!;
    [ObservableProperty] private string _clockText = string.Empty;
    [ObservableProperty] private int _notificationCount;
    [ObservableProperty] private FinancialYear? _selectedFinancialYear;
    [ObservableProperty] private ObservableCollection<FinancialYear> _financialYears = [];

    public string AppName => "Nickeltown Finance";
    public string AppVersion => AppInfo.VersionLabel;

    public MainViewModel(
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISessionService sessionService,
        IBackupService backupService,
        INotificationService notificationService,
        IFinancialYearService financialYearService,
        ISettingsService settingsService,
        IAppUpdateService appUpdateService,
        DashboardViewModel dashboardViewModel)
    {
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _sessionService = sessionService;
        _backupService = backupService;
        _notificationService = notificationService;
        _financialYearService = financialYearService;
        _settingsService = settingsService;
        _appUpdateService = appUpdateService;

        var user = _sessionService.CurrentUser;
        UserDisplayName = !string.IsNullOrWhiteSpace(user?.DisplayName)
            ? user!.DisplayName
            : user?.Username ?? "User";
        UserRoleDisplay = user?.Role.ToDisplayName() ?? string.Empty;
        IsAdministrator = user?.Role == UserRole.Administrator;
        LoadFinancialYears();
        StatusText = "Ready";
        ClockText = DateTime.Now.ToString("dd/MM/yyyy  •  HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        NotificationCount = 0;

        IsSidebarCollapsed = _settingsService.SidebarCollapsed;
        ApplySidebarWidth();
        RefreshBranding();

        _financialYearService.YearsChanged += OnFinancialYearsChanged;
        _financialYearService.ActiveYearChanged += OnFinancialYearsChanged;
        _navigationService.Navigated += OnNavigated;
        CurrentPage = dashboardViewModel;
        _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (_startupUpdateChecked || !UpdateConstants.IsConfigured)
            return;

        _startupUpdateChecked = true;

        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync();
            if (result.CheckSucceeded && result.UpdateAvailable && result.Update is not null)
            {
                _notificationService.ShowInfo(
                    $"Update available: v{result.Update.Version}. Open Settings → About & updates to install.");
            }
        }
        catch
        {
            // Silent on startup — manual check is available in Settings.
        }
    }

    public void Detach()
    {
        if (_detached)
            return;

        _detached = true;
        _financialYearService.YearsChanged -= OnFinancialYearsChanged;
        _financialYearService.ActiveYearChanged -= OnFinancialYearsChanged;
        _navigationService.Navigated -= OnNavigated;
    }

    [RelayCommand]
    private void SignOut()
    {
        if (!AppDialog.Confirm(
                "Sign out",
                "Sign out and switch to another user?",
                confirmText: "Sign out",
                cancelText: "Cancel"))
            return;

        Detach();

        if (System.Windows.Application.Current is App app)
            app.LogoutAndSwitchUser();
    }

    private void OnFinancialYearsChanged() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(RefreshFinancialYears);

    private void LoadFinancialYears()
    {
        var years = _financialYearService.GetAll();
        FinancialYears = new ObservableCollection<FinancialYear>(years);

        try
        {
            var active = _financialYearService.GetActiveYear();
            _suppressYearChange = true;
            SelectedFinancialYear = FinancialYears.FirstOrDefault(y => y.Id == active.Id) ?? active;
            _suppressYearChange = false;
            FinancialYearStatus = $"FY {active.Name}";
        }
        catch (InvalidOperationException)
        {
            _suppressYearChange = true;
            SelectedFinancialYear = FinancialYears.FirstOrDefault();
            _suppressYearChange = false;
            FinancialYearStatus = years.Count == 0 ? "No financial year" : "Select a year";
        }
    }

    partial void OnSelectedFinancialYearChanged(FinancialYear? value)
    {
        if (_suppressYearChange || value is null)
            return;

        _financialYearService.SetViewingYear(value);
        FinancialYearStatus = $"FY {value.Name}";
        StatusText = $"Viewing financial year {value.Name}";
        RefreshCurrentPage();
    }

    public void RefreshFinancialYears()
    {
        LoadFinancialYears();
        RefreshCurrentPage();
    }

    public void RefreshBranding()
    {
        ClubBranding.EnsureSettingsLogo(_settingsService);
        ClubName = _settingsService.ClubName;
        ClubLogoImage = ClubBranding.LoadImage(_settingsService.ClubLogoPath);
        WindowTitle = $"{AppName} — {ClubName}";
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        ApplySidebarWidth();
        _settingsService.SidebarCollapsed = IsSidebarCollapsed;
        _settingsService.Save();
    }

    private void ApplySidebarWidth() =>
        SidebarWidth = new GridLength(IsSidebarCollapsed ? 64 : 260);

    [RelayCommand]
    private void NavigateDashboard() => Navigate("Dashboard", "Dashboard", typeof(DashboardViewModel));

    [RelayCommand]
    private void NavigateTransactions() => Navigate("Transactions", "Transactions", typeof(TransactionsViewModel));

    [RelayCommand]
    private void NavigateImports() => Navigate("Imports", "Imports", typeof(ImportViewModel));

    [RelayCommand]
    private void NavigateReports() => Navigate("Reports", "Reports", typeof(ReportsViewModel));

    [RelayCommand]
    private void NavigateFinancialYears() => Navigate("FinancialYears", "Financial Years", typeof(FinancialYearsViewModel));

    [RelayCommand]
    private void NavigateSettings() => Navigate("Settings", "Settings", typeof(SettingsViewModel));

    // Legacy command names — route into the Imports section
    [RelayCommand]
    private void NavigateBanking() => NavigateImports();

    [RelayCommand]
    private void NavigateImport() => NavigateImports();

    [RelayCommand]
    private void NavigateMonthlyReport() => NavigateReports();

    [RelayCommand]
    private void NavigateAgmReport()
    {
        NavigateReports();
        if (CurrentPage is ReportsViewModel reports)
            reports.ShowAgmReport();
    }

    [RelayCommand]
    private void NavigateUsers()
    {
        NavigateSettings();
        if (CurrentPage is SettingsViewModel settings)
            settings.ShowSection("Users");
    }

    private void Navigate(string navKey, string pageName, Type viewModelType)
    {
        ActiveNavKey = navKey;
        CurrentPageName = pageName;
        _navigationService.Navigate(viewModelType);
    }

    [RelayCommand]
    private void NewTransaction()
    {
        var editor = _serviceProvider.GetRequiredService<TransactionEditorViewModel>();
        editor.Initialize(true);
        editor.Saved += (_, _) => RefreshCurrentPage();
        var window = new TransactionEditorWindow(editor)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void FocusSearch() => NavigateTransactions();

    [RelayCommand]
    private async Task BackupAsync()
    {
        var path = await _backupService.CreateBackupAsync(isManual: true);
        StatusText = $"Backup created: {Path.GetFileName(path)}";
        _notificationService.ShowSuccess("Backup created successfully.");
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshBranding();
        RefreshCurrentPage();
        ClockText = DateTime.Now.ToString("dd/MM/yyyy  •  HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        StatusText = $"Refreshed at {DateTime.Now:t}";
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (CurrentPage is TransactionsViewModel transactions)
            transactions.UndoDeleteCommand.Execute(null);
    }

    private void RefreshCurrentPage()
    {
        switch (CurrentPage)
        {
            case DashboardViewModel dashboard:
                _ = dashboard.LoadAsync();
                break;
            case TransactionsViewModel transactions:
                _ = transactions.LoadAsync();
                break;
            case ImportViewModel import:
                _ = import.RefreshHistoryCommand.ExecuteAsync(null);
                break;
            case ReceiptInboxViewModel inbox:
                _ = inbox.RefreshCommand.ExecuteAsync(null);
                break;
            case SettingsViewModel settings:
                RefreshBranding();
                _ = settings.LoadAsync();
                break;
            case FinancialYearsViewModel financialYears:
                _ = financialYears.LoadAsync();
                break;
            case ReportsViewModel:
                break;
        }
    }

    private void OnNavigated(Type viewModelType)
    {
        CurrentPage = _serviceProvider.GetRequiredService(viewModelType);

        (ActiveNavKey, CurrentPageName) = viewModelType.Name switch
        {
            nameof(DashboardViewModel) => ("Dashboard", "Dashboard"),
            nameof(TransactionsViewModel) => ("Transactions", "Transactions"),
            nameof(ImportViewModel) => ("Imports", "Imports"),
            nameof(ReceiptInboxViewModel) => ("Imports", "Receipt Inbox"),
            nameof(ReconciliationViewModel) => ("Imports", "Reconciliation"),
            nameof(ReportsViewModel) => ("Reports", "Reports"),
            nameof(MonthlyReportViewModel) => ("Reports", "Reports"),
            nameof(AgmReportViewModel) => ("Reports", "Reports"),
            nameof(FinancialYearsViewModel) => ("FinancialYears", "Financial Years"),
            nameof(SettingsViewModel) => ("Settings", "Settings"),
            nameof(UserManagementViewModel) => ("Settings", "Settings"),
            _ => (ActiveNavKey, CurrentPageName)
        };

        if (viewModelType == typeof(SettingsViewModel))
            RefreshBranding();
    }
}
