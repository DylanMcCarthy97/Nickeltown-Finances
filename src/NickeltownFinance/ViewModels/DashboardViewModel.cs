using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Services;
using NickeltownFinance.Views.Dialogs;
using SkiaSharp;

namespace NickeltownFinance.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboardService;
    private readonly INavigationService _navigationService;
    private readonly IBackupService _backupService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IServiceProvider _serviceProvider;
    private IReadOnlyList<ChartMonthItem> _chartSource = [];

    [ObservableProperty] private decimal _bankBalance;
    [ObservableProperty] private decimal _incomeThisMonth;
    [ObservableProperty] private decimal _expensesThisMonth;
    [ObservableProperty] private decimal _profitThisMonth;
    [ObservableProperty] private decimal _currentProfitLoss;
    [ObservableProperty] private decimal _yearToDateIncome;
    [ObservableProperty] private decimal _yearToDateExpenses;
    [ObservableProperty] private decimal _yearToDateProfit;
    [ObservableProperty] private int _transactionCount;
    [ObservableProperty] private string _financialYearName = string.Empty;
    [ObservableProperty] private DateTime _financialYearEndDate;
    [ObservableProperty] private int _daysUntilYearEnd;
    [ObservableProperty] private string _yearEndSummary = string.Empty;
    private List<TransactionListItem> _allRecentTransactions = [];

    [ObservableProperty] private ObservableCollection<TransactionListItem> _recentTransactions = [];
    [ObservableProperty] private ObservableCollection<ChartMonthItem> _monthlyChartData = [];
    [ObservableProperty] private ObservableCollection<string> _recentActivity = [];
    [ObservableProperty] private int _receiptsAttached;
    [ObservableProperty] private int _missingReceipts;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _clubName = string.Empty;

    [ObservableProperty] private int _recentImportCount;
    [ObservableProperty] private int _transactionsWaitingReview;
    [ObservableProperty] private int _squareDepositsWaitingMatch;
    [ObservableProperty] private int _duplicatesFound;
    [ObservableProperty] private string _lastBankImportText = "Never";
    [ObservableProperty] private string _lastSquareImportText = "Never";

    [ObservableProperty] private ISeries[] _cashFlowSeries = [];
    [ObservableProperty] private Axis[] _cashFlowXAxes = [];
    [ObservableProperty] private Axis[] _cashFlowYAxes = [];
    [ObservableProperty] private SolidColorPaint _legendTextPaint = new(SKColors.Gray);
    [ObservableProperty] private SolidColorPaint _tooltipBackgroundPaint = new(SKColors.DimGray);
    [ObservableProperty] private SolidColorPaint _tooltipTextPaint = new(SKColors.White);

    public DashboardViewModel(
        IDashboardService dashboardService,
        INavigationService navigationService,
        IBackupService backupService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IThemeService themeService,
        IServiceProvider serviceProvider)
    {
        _dashboardService = dashboardService;
        _navigationService = navigationService;
        _backupService = backupService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _serviceProvider = serviceProvider;
        _themeService.ThemeChanged += OnThemeChanged;
        _ = LoadAsync();
    }

    private void OnThemeChanged()
    {
        if (_chartSource.Count > 0)
            BuildCashFlowChart(_chartSource);
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            ClubName = _settingsService.ClubName;
            var summary = await _dashboardService.GetSummaryAsync();
            BankBalance = summary.BankBalance;
            IncomeThisMonth = summary.IncomeThisMonth;
            ExpensesThisMonth = summary.ExpensesThisMonth;
            ProfitThisMonth = summary.ProfitThisMonth;
            CurrentProfitLoss = summary.CurrentProfitLoss;
            YearToDateIncome = summary.YearToDateIncome;
            YearToDateExpenses = summary.YearToDateExpenses;
            YearToDateProfit = summary.YearToDateProfit;
            TransactionCount = summary.TransactionCount;
            FinancialYearName = summary.CurrentFinancialYearName;
            FinancialYearEndDate = summary.FinancialYearEndDate;
            DaysUntilYearEnd = summary.DaysUntilYearEnd;
            YearEndSummary = DaysUntilYearEnd == 0
                ? $"Year ends {FinancialYearEndDate:dd/MM/yyyy}"
                : $"Year ends {FinancialYearEndDate:dd/MM/yyyy} · {DaysUntilYearEnd} days left";
            _allRecentTransactions = summary.RecentTransactions.ToList();
            ApplySearch();
            MonthlyChartData = new ObservableCollection<ChartMonthItem>(summary.MonthlyChartData);
            BuildCashFlowChart(summary.MonthlyChartData);
            RecentActivity = new ObservableCollection<string>(summary.RecentActivity);
            ReceiptsAttached = summary.ReceiptsAttached;
            MissingReceipts = summary.MissingReceipts;

            var imports = summary.ImportStatus;
            RecentImportCount = imports.RecentImportCount;
            TransactionsWaitingReview = imports.TransactionsWaitingReview;
            SquareDepositsWaitingMatch = imports.SquareDepositsWaitingMatch;
            DuplicatesFound = imports.DuplicatesFound;
            LastBankImportText = imports.LastBankImport is { } bank
                ? bank.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : "Never";
            LastSquareImportText = imports.LastSquareImport is { } square
                ? square.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : "Never";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildCashFlowChart(IReadOnlyList<ChartMonthItem> months)
    {
        _chartSource = months;
        var labels = months.Select(m => m.Label).ToArray();
        var income = months.Select(m => (double)m.Income).ToArray();
        var expenses = months.Select(m => (double)m.Expenses).ToArray();

        var isDark = _themeService.IsDark;
        var muted = SKColor.Parse(isDark ? "#7A8794" : "#6B7684");
        var grid = SKColor.Parse(isDark ? "#262D3A" : "#E4E9F0");
        var tooltipBg = SKColor.Parse(isDark ? "#2A3140" : "#FFFFFF");
        var tooltipFg = SKColor.Parse(isDark ? "#FFFFFF" : "#14171F");

        LegendTextPaint = new SolidColorPaint(muted);
        TooltipBackgroundPaint = new SolidColorPaint(tooltipBg);
        TooltipTextPaint = new SolidColorPaint(tooltipFg);

        CashFlowSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Income",
                Values = income,
                Fill = new SolidColorPaint(SKColor.Parse("#22C55E")),
                MaxBarWidth = 18,
                Rx = 4,
                Ry = 4
            },
            new ColumnSeries<double>
            {
                Name = "Expenses",
                Values = expenses,
                Fill = new SolidColorPaint(SKColor.Parse("#EF4444")),
                MaxBarWidth = 18,
                Rx = 4,
                Ry = 4
            }
        ];

        CashFlowXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(muted),
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        ];

        CashFlowYAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(muted),
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(grid)
                {
                    StrokeThickness = 1
                }
            }
        ];
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var items = string.IsNullOrEmpty(query)
            ? _allRecentTransactions
            : _allRecentTransactions.Where(t =>
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.CategoryName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Reference?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        RecentTransactions = new ObservableCollection<TransactionListItem>(items);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void NewIncome() => OpenEditor(true);

    [RelayCommand]
    private void NewExpense() => OpenEditor(false);

    [RelayCommand]
    private void AddTransaction() => OpenEditor(false);

    [RelayCommand]
    private void GoImport()
    {
        _navigationService.Navigate<ImportViewModel>();
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel main
            && main.CurrentPage is ImportViewModel import)
            import.StartAnzImportCommand.Execute(null);
    }

    [RelayCommand]
    private void GoTransactions() => _navigationService.Navigate<TransactionsViewModel>();

    [RelayCommand]
    private void GoReports() => _navigationService.Navigate<ReportsViewModel>();

    [RelayCommand]
    private void GoAgmReport()
    {
        _navigationService.Navigate<ReportsViewModel>();
        // ReportsViewModel is resolved on navigate; open AGM tab via main window if available.
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel main
            && main.CurrentPage is ReportsViewModel reports)
            reports.ShowAgmReport();
    }

    [RelayCommand]
    private void GoSettings() => _navigationService.Navigate<SettingsViewModel>();

    [RelayCommand]
    private async Task BackupAsync()
    {
        var path = await _backupService.CreateBackupAsync(isManual: true);
        _notificationService.ShowSuccess($"Backup created: {Path.GetFileName(path)}");
    }

    private void OpenEditor(bool isIncome)
    {
        var editor = _serviceProvider.GetRequiredService<TransactionEditorViewModel>();
        editor.Initialize(isIncome);
        editor.Saved += async (_, _) => await LoadAsync();
        var window = new TransactionEditorWindow(editor)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }
}
