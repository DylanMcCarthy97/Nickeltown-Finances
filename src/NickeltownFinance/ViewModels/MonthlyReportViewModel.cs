using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;

namespace NickeltownFinance.ViewModels;

public partial class MonthlyReportViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly IFinancialYearService _financialYearService;
    private readonly INotificationService _notificationService;

    [ObservableProperty] private ObservableCollection<FinancialYear> _financialYears = [];
    [ObservableProperty] private FinancialYear? _selectedFinancialYear;
    [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private MonthlyReportData? _reportData;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private string _searchText = string.Empty;

    public int[] Months => Enumerable.Range(1, 12).ToArray();

    public IReadOnlyList<CategoryTotal> VisibleIncome => FilterCategories(ReportData?.IncomeByCategory);
    public IReadOnlyList<CategoryTotal> VisibleExpenses => FilterCategories(ReportData?.ExpensesByCategory);

    partial void OnSearchTextChanged(string value) => NotifyVisibleCategories();
    partial void OnReportDataChanged(MonthlyReportData? value) => NotifyVisibleCategories();

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
        INotificationService notificationService)
    {
        _reportService = reportService;
        _financialYearService = financialYearService;
        _notificationService = notificationService;
        LoadFinancialYears();
    }

    private void LoadFinancialYears()
    {
        FinancialYears = new ObservableCollection<FinancialYear>(_financialYearService.GetAll());
        SelectedFinancialYear = _financialYearService.GetActiveYear();
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (SelectedFinancialYear is null) return;

        IsBusy = true;
        try
        {
            ReportData = await _reportService.BuildMonthlyReportAsync(
                SelectedFinancialYear.Id, SelectedYear, SelectedMonth, Notes);
            HasReport = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (ReportData is null) return;
        ReportData.PrintedAt = DateTime.Now;
        var path = Path.Combine(AppPaths.ExportsPath, $"MonthlyReport_{SelectedYear}_{SelectedMonth:D2}.pdf");
        await _reportService.ExportMonthlyPdfAsync(ReportData, path);
        _notificationService.ShowSuccess($"PDF saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (ReportData is null) return;
        ReportData.PrintedAt = DateTime.Now;
        var path = Path.Combine(AppPaths.ExportsPath, $"MonthlyReport_{SelectedYear}_{SelectedMonth:D2}.xlsx");
        await _reportService.ExportMonthlyExcelAsync(ReportData, path);
        _notificationService.ShowSuccess($"Excel saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        await ExportPdfAsync();
    }
}
