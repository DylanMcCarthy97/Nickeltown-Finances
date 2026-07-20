using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
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

    [ObservableProperty] private ObservableCollection<FinancialYear> _financialYears = [];
    [ObservableProperty] private FinancialYear? _selectedFinancialYear;
    [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private decimal _cashOnHand;
    [ObservableProperty] private decimal _shireBonds;
    [ObservableProperty] private decimal _payPalBalance;
    [ObservableProperty] private MonthlyReportData? _reportData;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<MonthDocumentInfo> _pitstopReports = [];
    [ObservableProperty] private MonthDocumentInfo? _selectedPitstopReport;

    public int[] Months => Enumerable.Range(1, 12).ToArray();

    public bool HasPitstopReports => PitstopReports.Count > 0;

    public IReadOnlyList<CategoryTotal> VisibleIncome => FilterCategories(ReportData?.IncomeByCategory);
    public IReadOnlyList<CategoryTotal> VisibleExpenses => FilterCategories(ReportData?.ExpensesByCategory);

    partial void OnSearchTextChanged(string value) => NotifyVisibleCategories();
    partial void OnSelectedMonthChanged(int value) => _ = ReloadPitstopReportsAsync();
    partial void OnSelectedYearChanged(int value) => _ = ReloadPitstopReportsAsync();

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
        _ = ReloadPitstopReportsAsync();
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
    private async Task GenerateReportAsync()
    {
        if (SelectedFinancialYear is null) return;

        BeginBusy("On track — building monthly report…");
        try
        {
            ReportData = await _reportService.BuildMonthlyReportAsync(
                SelectedFinancialYear.Id, SelectedYear, SelectedMonth, Notes);
            ApplyHoldingsToReport();
            HasReport = true;
            await ReloadPitstopReportsAsync();
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
            var path = Path.Combine(AppPaths.ExportsPath, $"MonthlyReport_{SelectedYear}_{SelectedMonth:D2}.pdf");
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
            var path = Path.Combine(AppPaths.ExportsPath, $"MonthlyReport_{SelectedYear}_{SelectedMonth:D2}.xlsx");
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
