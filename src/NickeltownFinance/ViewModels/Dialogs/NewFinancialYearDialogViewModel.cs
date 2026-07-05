using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class NewFinancialYearDialogViewModel : ViewModelBase
{
    private readonly IFinancialYearService _financialYearService;

    [ObservableProperty] private string _financialYearName = string.Empty;
    [ObservableProperty] private DateTime _openingDate = DateTime.Today;
    [ObservableProperty] private string _openingBalanceText = string.Empty;
    [ObservableProperty] private bool _carryForwardPreviousClosingBalance;
    [ObservableProperty] private bool _isOpeningBalanceEnabled = true;
    [ObservableProperty] private string _carryForwardHint = string.Empty;

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public NewFinancialYearDialogViewModel(IFinancialYearService financialYearService) =>
        _financialYearService = financialYearService;

    public void Initialize()
    {
        var active = _financialYearService.GetCurrent() ?? _financialYearService.GetAll().FirstOrDefault();
        if (active is not null)
        {
            OpeningDate = active.EndDate.AddDays(1);
            var end = OpeningDate.AddYears(1).AddDays(-1);
            FinancialYearName = $"{OpeningDate.Year}-{end.Year % 100:D2}";
            var closing = _financialYearService.GetClosingBalance(active.Id);
            CarryForwardHint = $"Previous closing balance: {closing:C}";
        }
        else
        {
            var startMonth = 7;
            var today = DateTime.Today;
            var year = today.Month >= startMonth ? today.Year : today.Year - 1;
            OpeningDate = new DateTime(year, startMonth, 1);
            var end = OpeningDate.AddYears(1).AddDays(-1);
            FinancialYearName = $"{OpeningDate.Year}-{end.Year % 100:D2}";
            CarryForwardHint = string.Empty;
        }

        CarryForwardPreviousClosingBalance = false;
        OpeningBalanceText = string.Empty;
        IsOpeningBalanceEnabled = true;
        ErrorMessage = null;
    }

    partial void OnCarryForwardPreviousClosingBalanceChanged(bool value)
    {
        IsOpeningBalanceEnabled = !value;
        if (!value) return;

        var previous = _financialYearService.GetAll()
            .Where(y => y.OpeningDate < OpeningDate.Date)
            .OrderByDescending(y => y.OpeningDate)
            .FirstOrDefault()
            ?? _financialYearService.GetAll().OrderByDescending(y => y.OpeningDate).FirstOrDefault();

        if (previous is null)
        {
            OpeningBalanceText = string.Empty;
            CarryForwardHint = "No previous financial year is available.";
            return;
        }

        var closing = _financialYearService.GetClosingBalance(previous.Id);
        OpeningBalanceText = closing.ToString("0.00");
        CarryForwardHint = $"Previous closing balance ({previous.Name}): {closing:C}";
    }

    partial void OnOpeningDateChanged(DateTime value)
    {
        if (value == default) return;
        var end = value.Date.AddYears(1).AddDays(-1);
        FinancialYearName = $"{value.Year}-{end.Year % 100:D2}";
        if (CarryForwardPreviousClosingBalance)
            OnCarryForwardPreviousClosingBalanceChanged(true);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FinancialYearName))
        {
            ErrorMessage = "Financial year is required.";
            return;
        }

        if (OpeningDate == default)
        {
            ErrorMessage = "Starting date must be a valid date.";
            return;
        }

        decimal openingBalance = 0;
        if (!CarryForwardPreviousClosingBalance)
        {
            if (string.IsNullOrWhiteSpace(OpeningBalanceText))
            {
                ErrorMessage = "Starting balance is required.";
                return;
            }

            if (!decimal.TryParse(OpeningBalanceText.Replace("$", "").Replace(",", "").Trim(), out openingBalance))
            {
                ErrorMessage = "Starting balance must be a valid amount.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _financialYearService.CreateAsync(new CreateFinancialYearRequest
            {
                Name = FinancialYearName.Trim(),
                OpeningDate = OpeningDate.Date,
                OpeningBalance = openingBalance,
                CarryForwardPreviousClosingBalance = CarryForwardPreviousClosingBalance
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

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
