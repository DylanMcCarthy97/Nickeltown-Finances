using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NickeltownFinance.ViewModels;

/// <summary>
/// Reports hub — Monthly, Year/AGM, and category summaries in one place.
/// </summary>
public partial class ReportsViewModel : ViewModelBase
{
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _searchText = string.Empty;

    public MonthlyReportViewModel MonthlyReport { get; }
    public AgmReportViewModel AgmReport { get; }

    public ReportsViewModel(MonthlyReportViewModel monthlyReport, AgmReportViewModel agmReport)
    {
        MonthlyReport = monthlyReport;
        AgmReport = agmReport;
    }

    partial void OnSearchTextChanged(string value)
    {
        MonthlyReport.SearchText = value;
        AgmReport.SearchText = value;
    }

    [RelayCommand]
    private void ShowMonthly() => SelectedTabIndex = 0;

    [RelayCommand]
    private void ShowAgm() => SelectedTabIndex = 1;

    public void ShowMonthlyReport() => SelectedTabIndex = 0;

    public void ShowAgmReport() => SelectedTabIndex = 1;
}
