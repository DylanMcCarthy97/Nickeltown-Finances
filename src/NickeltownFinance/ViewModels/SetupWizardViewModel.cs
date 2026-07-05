using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly IDataSeederService _dataSeederService;

    [ObservableProperty] private string _clubName = string.Empty;
    [ObservableProperty] private MonthOption? _selectedStartMonth;
    [ObservableProperty] private DateTime _startingDate = DateTime.Today;
    [ObservableProperty] private string _bankBalanceText = string.Empty;
    [ObservableProperty] private string _financialYearPreview = string.Empty;

    public ObservableCollection<MonthOption> StartMonths { get; } = new(
        Enumerable.Range(1, 12).Select(m => new MonthOption(m, new DateTime(2000, m, 1).ToString("MMMM"))));

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public SetupWizardViewModel(IDataSeederService dataSeederService)
    {
        _dataSeederService = dataSeederService;
        SelectedStartMonth = StartMonths.First(m => m.Number == FinancialYearPeriod.DefaultStartMonth);
        StartingDate = DateTime.Today;
        UpdateFinancialYearPreview();
    }

    partial void OnSelectedStartMonthChanged(MonthOption? value) => UpdateFinancialYearPreview();

    partial void OnStartingDateChanged(DateTime value) => UpdateFinancialYearPreview();

    private void UpdateFinancialYearPreview()
    {
        var month = SelectedStartMonth?.Number ?? FinancialYearPeriod.DefaultStartMonth;
        var date = StartingDate == default ? DateTime.Today : StartingDate.Date;
        var (_, _, name) = FinancialYearPeriod.ForDate(date, month);
        FinancialYearPreview = name;
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(ClubName))
        {
            ErrorMessage = "Club name is required.";
            return;
        }

        if (SelectedStartMonth is null)
        {
            ErrorMessage = "Financial year start month is required.";
            return;
        }

        if (StartingDate == default)
        {
            ErrorMessage = "Starting date must be a valid date.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BankBalanceText))
        {
            ErrorMessage = "Current bank balance is required.";
            return;
        }

        if (!decimal.TryParse(BankBalanceText.Replace("$", "").Replace(",", "").Trim(),
                out var bankBalance))
        {
            ErrorMessage = "Bank balance must be a valid amount.";
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _dataSeederService.CompleteFirstRunSetupAsync(new FirstRunSetupRequest
            {
                ClubName = ClubName.Trim(),
                FinancialYearStartMonth = SelectedStartMonth.Number,
                StartingDate = StartingDate.Date,
                StartingBalance = bankBalance
            });

            if (!success)
            {
                ErrorMessage = error;
                return;
            }

            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class MonthOption(int number, string name)
{
    public int Number { get; } = number;
    public string Name { get; } = name;
    public override string ToString() => Name;
}
