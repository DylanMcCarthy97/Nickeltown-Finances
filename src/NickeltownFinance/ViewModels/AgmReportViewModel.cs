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

public partial class AgmReportViewModel : ViewModelBase
{
    private readonly IReportService _reportService;
    private readonly IFinancialYearService _financialYearService;
    private readonly INotificationService _notificationService;

    [ObservableProperty] private ObservableCollection<FinancialYear> _financialYears = [];
    [ObservableProperty] private FinancialYear? _selectedFinancialYear;
    [ObservableProperty] private AgmReportData? _reportData;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private string _searchText = string.Empty;

    public IReadOnlyList<CategoryTotal> VisibleIncome => FilterCategories(ReportData?.IncomeByCategory);
    public IReadOnlyList<CategoryTotal> VisibleExpenses => FilterCategories(ReportData?.ExpensesByCategory);
    public IReadOnlyList<MonthlyBreakdown> VisibleMonths => FilterMonths(ReportData?.MonthlyData);

    partial void OnSearchTextChanged(string value) => NotifyVisibleCollections();
    partial void OnReportDataChanged(AgmReportData? value) => NotifyVisibleCollections();

    private void NotifyVisibleCollections()
    {
        OnPropertyChanged(nameof(VisibleIncome));
        OnPropertyChanged(nameof(VisibleExpenses));
        OnPropertyChanged(nameof(VisibleMonths));
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

    private IReadOnlyList<MonthlyBreakdown> FilterMonths(IReadOnlyList<MonthlyBreakdown>? source)
    {
        if (source is null || source.Count == 0)
            return [];
        var query = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
            return source;
        return source.Where(m => m.MonthName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public AgmReportViewModel(
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
            ReportData = await _reportService.BuildAgmReportAsync(SelectedFinancialYear.Id);
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
        if (ReportData is null || SelectedFinancialYear is null) return;
        ReportData.PrintedAt = DateTime.Now;
        var path = Path.Combine(AppPaths.ExportsPath, $"AGMReport_{SelectedFinancialYear.Name.Replace("/", "-")}.pdf");
        await _reportService.ExportAgmPdfAsync(ReportData, path);
        _notificationService.ShowSuccess($"PDF saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (ReportData is null || SelectedFinancialYear is null) return;
        ReportData.PrintedAt = DateTime.Now;
        var path = Path.Combine(AppPaths.ExportsPath, $"AGMReport_{SelectedFinancialYear.Name.Replace("/", "-")}.xlsx");
        await _reportService.ExportAgmExcelAsync(ReportData, path);
        _notificationService.ShowSuccess($"Excel saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task PrintAsync() => await ExportPdfAsync();

    [RelayCommand]
    private async Task ExportCategorySummaryExcelAsync()
    {
        if (SelectedFinancialYear is null) return;
        var path = Path.Combine(AppPaths.ExportsPath,
            $"CategorySummary_{SelectedFinancialYear.Name.Replace("/", "-")}.xlsx");
        await _reportService.ExportCategorySummaryExcelAsync(SelectedFinancialYear.Id, path);
        _notificationService.ShowSuccess($"Category summary saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportGstSummaryExcelAsync()
    {
        if (SelectedFinancialYear is null) return;
        var path = Path.Combine(AppPaths.ExportsPath,
            $"GstSummary_{SelectedFinancialYear.Name.Replace("/", "-")}.xlsx");
        await _reportService.ExportGstSummaryExcelAsync(SelectedFinancialYear.Id, path);
        _notificationService.ShowSuccess($"GST summary saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ExportReceiptAuditExcelAsync()
    {
        if (SelectedFinancialYear is null) return;
        var path = Path.Combine(AppPaths.ExportsPath,
            $"ReceiptAudit_{SelectedFinancialYear.Name.Replace("/", "-")}.xlsx");
        await _reportService.ExportReceiptAuditExcelAsync(SelectedFinancialYear.Id, path);
        _notificationService.ShowSuccess($"Receipt audit saved to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
