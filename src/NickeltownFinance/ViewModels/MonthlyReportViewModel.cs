using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class MonthlyReportViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly IFinancialYearService _financialYearService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IMonthDocumentService _monthDocumentService;
    private readonly IServiceProvider _serviceProvider;
    private bool _syncingDates;

    [ObservableProperty] private ObservableCollection<FinancialYear> _financialYears = [];
    [ObservableProperty] private FinancialYear? _selectedFinancialYear;
    [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private decimal _cashOnHand;
    [ObservableProperty] private decimal _shireBonds;
    [ObservableProperty] private decimal _payPalBalance;
    [ObservableProperty] private MonthlyReportData? _reportData;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<MonthDocumentInfo> _pitstopReports = [];
    [ObservableProperty] private MonthDocumentInfo? _selectedPitstopReport;
    [ObservableProperty] private ObservableCollection<AnzStatementOption> _anzStatements = [];
    [ObservableProperty] private AnzStatementOption? _selectedAnzStatement;
    [ObservableProperty] private string _anzOpeningText = string.Empty;
    [ObservableProperty] private string _anzClosingText = string.Empty;
    [ObservableProperty] private StatementReconcileResult? _anzReconcile;

    public int[] Months => Enumerable.Range(1, 12).ToArray();

    public bool HasPitstopReports => PitstopReports.Count > 0;

    public bool HasAnzStatements => AnzStatements.Count > 0;

    public bool HasAnzReconcile => AnzReconcile is not null;

    public bool IsStatementPeriod =>
        FromDate is { } from &&
        ToDate is { } to &&
        StatementPeriod.Matches(from, to, SelectedYear, SelectedMonth);

    public string PeriodHint => IsStatementPeriod
        ? "ANZ statement cycle — 22nd to the 21st. Reconcile opening and closing against the imported statement."
        : "Custom date range. Opening and closing are the ANZ ledger totals for these dates.";

    public IReadOnlyList<CategoryTotal> VisibleIncome => FilterCategories(ReportData?.IncomeByCategory);
    public IReadOnlyList<CategoryTotal> VisibleExpenses => FilterCategories(ReportData?.ExpensesByCategory);

    partial void OnSearchTextChanged(string value) => NotifyVisibleCategories();
    partial void OnSelectedMonthChanged(int value)
    {
        if (!_syncingDates)
            ApplyStatementPeriodDates();
        _ = ReloadPitstopReportsAsync();
    }
    partial void OnSelectedYearChanged(int value)
    {
        if (!_syncingDates)
            ApplyStatementPeriodDates();
        _ = ReloadPitstopReportsAsync();
    }
    partial void OnFromDateChanged(DateTime? value) => NotifyPeriodHint();
    partial void OnToDateChanged(DateTime? value) => NotifyPeriodHint();
    partial void OnSelectedAnzStatementChanged(AnzStatementOption? value) => PrefillAnzBalances(value);
    partial void OnAnzReconcileChanged(StatementReconcileResult? value) =>
        OnPropertyChanged(nameof(HasAnzReconcile));

    partial void OnReportDataChanged(MonthlyReportData? value)
    {
        if (value is not null)
        {
            CashOnHand = value.CashOnHand;
            ShireBonds = value.ShireBonds;
            PayPalBalance = value.PayPalBalance;
        }
        NotifyVisibleCategories();
    }

    public decimal TotalFundsOwned =>
        (ReportData?.ClosingBalance ?? 0m) + CashOnHand + ShireBonds + PayPalBalance;

    private void NotifyVisibleCategories()
    {
        OnPropertyChanged(nameof(VisibleIncome));
        OnPropertyChanged(nameof(VisibleExpenses));
    }

    private IReadOnlyList<CategoryTotal> FilterCategories(IReadOnlyList<CategoryTotal>? source)
    {
        if (source is null || source.Count == 0)
            return [];
        var query = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
            return source;
        return source.Where(c => c.CategoryName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public MonthlyReportViewModel(
        IReportService reportService,
        IFinancialYearService financialYearService,
        ISettingsService settingsService,
        INotificationService notificationService,
        IMonthDocumentService monthDocumentService,
        IServiceProvider serviceProvider)
    {
        _reportService = reportService;
        _financialYearService = financialYearService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _monthDocumentService = monthDocumentService;
        _serviceProvider = serviceProvider;
        CashOnHand = _settingsService.DefaultCashOnHand;
        ShireBonds = _settingsService.DefaultShireBonds;
        PayPalBalance = _settingsService.DefaultPayPalBalance;
        LoadFinancialYears();
        ApplyStatementPeriodDates();
        _ = ReloadPitstopReportsAsync();
        _ = LoadAnzStatementsAsync();
    }

    private void ApplyStatementPeriodDates()
    {
        if (SelectedMonth is < 1 or > 12)
            return;

        var (start, end) = StatementPeriod.ForMonth(SelectedYear, SelectedMonth);
        FromDate = start;
        ToDate = end;
        NotifyPeriodHint();
    }

    private void NotifyPeriodHint()
    {
        OnPropertyChanged(nameof(IsStatementPeriod));
        OnPropertyChanged(nameof(PeriodHint));
    }

    private async Task LoadAnzStatementsAsync()
    {
        try
        {
            var selectedId = SelectedAnzStatement?.BatchId;
            var statements = await _reportService.GetAnzStatementsAsync();
            AnzStatements = new ObservableCollection<AnzStatementOption>(statements);
            OnPropertyChanged(nameof(HasAnzStatements));

            SelectedAnzStatement = selectedId is { } id && id != default
                ? AnzStatements.FirstOrDefault(s => s.BatchId == id)
                : AnzStatements.FirstOrDefault(s =>
                    FromDate is { } from &&
                    ToDate is { } to &&
                    s.PeriodStart == from.Date &&
                    s.PeriodEnd == to.Date);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void PrefillAnzBalances(AnzStatementOption? statement)
    {
        if (statement is null)
            return;

        if (string.IsNullOrWhiteSpace(AnzOpeningText) && statement.StatementOpening is { } opening)
            AnzOpeningText = opening.ToString("0.00");
        if (string.IsNullOrWhiteSpace(AnzClosingText) && statement.StatementClosing is { } closing)
            AnzClosingText = closing.ToString("0.00");
    }

    private void ApplyAnzStatementDates(AnzStatementOption statement)
    {
        _syncingDates = true;
        try
        {
            SelectedYear = statement.PeriodEnd.Year;
            SelectedMonth = statement.PeriodEnd.Month;
            FromDate = statement.PeriodStart;
            ToDate = statement.PeriodEnd;
        }
        finally
        {
            _syncingDates = false;
            NotifyPeriodHint();
        }
    }

    private void LoadFinancialYears()
    {
        FinancialYears = new ObservableCollection<FinancialYear>(_financialYearService.GetAll());
        SelectedFinancialYear = _financialYearService.GetActiveYear();
    }

    private async Task ReloadPitstopReportsAsync()
    {
        try
        {
            var reports = await _monthDocumentService.GetForMonthAsync(SelectedYear, SelectedMonth);
            PitstopReports = new ObservableCollection<MonthDocumentInfo>(reports);
            SelectedPitstopReport = PitstopReports.FirstOrDefault();
            OnPropertyChanged(nameof(HasPitstopReports));

            if (ReportData is not null)
                ReportData.PitstopReports = reports;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void UseStatementPeriod() => ApplyStatementPeriodDates();

    [RelayCommand]
    private async Task ReconcileAnzStatementAsync()
    {
        if (SelectedAnzStatement is null)
        {
            _notificationService.ShowError("Import an ANZ statement first, then choose it here.");
            return;
        }

        PrefillAnzBalances(SelectedAnzStatement);
        ApplyAnzStatementDates(SelectedAnzStatement);
        await GenerateReportAsync();
        NotifyAnzReconcileOutcome();
    }

    [RelayCommand]
    private void CheckAnzBalances()
    {
        if (ReportData is null)
        {
            _notificationService.ShowError("Preview the report first, then check the ANZ balances.");
            return;
        }

        ApplyAnzReconcile();
        NotifyAnzReconcileOutcome();
    }

    private void ApplyAnzReconcile()
    {
        if (ReportData is null)
            return;

        if (!TryParseMoney(AnzOpeningText, out var opening, required: false))
        {
            _notificationService.ShowError("ANZ opening must be a valid amount.");
            return;
        }

        if (!TryParseMoney(AnzClosingText, out var closing, required: false))
        {
            _notificationService.ShowError("ANZ closing must be a valid amount.");
            return;
        }

        AnzReconcile = _reportService.ReconcileWithAnz(
            ReportData,
            SelectedAnzStatement,
            opening,
            closing);
        OnPropertyChanged(nameof(HasAnzReconcile));
    }

    private void NotifyAnzReconcileOutcome()
    {
        if (AnzReconcile is null)
            return;

        if (AnzReconcile.IsFullyReconciled)
            _notificationService.ShowSuccess("ANZ statement matches this report.");
        else if (AnzReconcile.State == "Mismatch")
            _notificationService.ShowError(AnzReconcile.Summary);
        else if (AnzReconcile.State == "NeedsBalances")
            _notificationService.ShowInfo("Type the opening and closing from the ANZ statement, then click Check balances.");
    }

    private static bool TryParseMoney(string? text, out decimal? value, bool required)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
            return !required;

        if (!decimal.TryParse(text.Replace("$", "").Replace(",", "").Trim(), out var parsed))
            return false;

        value = parsed;
        return true;
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedFinancialYear is null) return;

        if (FromDate is null || ToDate is null)
            ApplyStatementPeriodDates();

        if (FromDate is not { } from || ToDate is not { } to)
            return;

        if (to.Date < from.Date)
        {
            const string message = "The report end date must be on or after the start date.";
            ErrorMessage = message;
            _notificationService.ShowError(message);
            return;
        }

        BeginBusy("On track — building monthly report…");
        try
        {
            ReportData = await _reportService.BuildMonthlyReportAsync(
                SelectedFinancialYear.Id,
                SelectedYear,
                SelectedMonth,
                Notes,
                from.Date,
                to.Date);
            ApplyHoldingsToReport();
            ApplyAnzReconcile();
            HasReport = true;
            await ReloadPitstopReportsAsync();
            await LoadAnzStatementsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private async Task AttachPitstopReportAsync()
    {
        var extensions = string.Join(";", _monthDocumentService.SupportedExtensions.Select(e => $"*{e}"));
        var dialog = new OpenFileDialog
        {
            Title = "Attach Pitstop end-of-day report",
            Filter = $"Supported files|{extensions}|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
            return;

        BeginBusy("Pit stop — attaching event report…");
        try
        {
            foreach (var file in dialog.FileNames)
            {
                await _monthDocumentService.AddAsync(
                    SelectedYear,
                    SelectedMonth,
                    file,
                    MonthDocumentKind.PitstopReport);
            }

            await ReloadPitstopReportsAsync();
            _notificationService.ShowSuccess(
                dialog.FileNames.Length == 1
                    ? "Pitstop report attached."
                    : $"{dialog.FileNames.Length} Pitstop reports attached.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private void OpenPitstopReport(MonthDocumentInfo? document)
    {
        document ??= SelectedPitstopReport;
        if (document is null)
            return;

        if (!File.Exists(document.FullPath))
        {
            _notificationService.ShowError("The attached file could not be found.");
            return;
        }

        var viewer = _serviceProvider.GetRequiredService<ReceiptViewerViewModel>();
        viewer.Load([_monthDocumentService.ToAttachmentInfo(document)]);
        var window = new ReceiptViewerWindow(viewer)
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = document.DisplayLabel
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenPitstopReportExternally(MonthDocumentInfo? document)
    {
        document ??= SelectedPitstopReport;
        if (document is null || !File.Exists(document.FullPath))
            return;

        Process.Start(new ProcessStartInfo(document.FullPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task RemovePitstopReportAsync(MonthDocumentInfo? document)
    {
        document ??= SelectedPitstopReport;
        if (document is null)
            return;

        if (!System.Windows.MessageBox.Show(
                $"Remove \"{document.DisplayLabel}\" from {SelectedMonth}/{SelectedYear}?",
                "Remove Pitstop report",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question).Equals(System.Windows.MessageBoxResult.Yes))
            return;

        try
        {
            await _monthDocumentService.DeleteAsync(document.Id);
            await ReloadPitstopReportsAsync();
            _notificationService.ShowSuccess("Pitstop report removed.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData is null) return;
        BeginBusy("Finish line — exporting PDF…");
        try
        {
            ApplyHoldingsToReport();
            _reportService.ApplyPrintDate(ReportData);
            var path = Path.Combine(AppPaths.ExportsPath, BuildExportFileName("pdf"));
            await _reportService.ExportMonthlyPdfAsync(ReportData, path);
            _notificationService.ShowSuccess($"PDF saved to {path}");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (ReportData is null) return;
        BeginBusy("Finish line — exporting Excel…");
        try
        {
            ApplyHoldingsToReport();
            _reportService.ApplyPrintDate(ReportData);
            var path = Path.Combine(AppPaths.ExportsPath, BuildExportFileName("xlsx"));
            await _reportService.ExportMonthlyExcelAsync(ReportData, path);
            _notificationService.ShowSuccess($"Excel saved to {path}");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        await ExportPdfAsync();
    }

    private string BuildExportFileName(string extension)
    {
        var start = ReportData?.PeriodStart ?? FromDate ?? new DateTime(SelectedYear, SelectedMonth, 1);
        var end = ReportData?.PeriodEnd ?? ToDate ?? start;
        return $"MonthlyReport_{start:yyyyMMdd}_{end:yyyyMMdd}.{extension}";
    }

    private void ApplyHoldingsToReport()
    {
        if (ReportData is null) return;
        ReportData.CashOnHand = CashOnHand;
        ReportData.ShireBonds = ShireBonds;
        ReportData.PayPalBalance = PayPalBalance;
        ReportData.PitstopReports = PitstopReports.ToList();
        OnPropertyChanged(nameof(TotalFundsOwned));
    }
}
