using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class EditOpeningBalanceDialogViewModel : ViewModelBase
{
    private readonly IFinancialYearService _financialYearService;
    private ObjectId _id = ObjectId.Empty;

    [ObservableProperty] private string _financialYearName = string.Empty;
    [ObservableProperty] private DateTime _openingDate = DateTime.Today;
    [ObservableProperty] private string _openingBalanceText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public EditOpeningBalanceDialogViewModel(IFinancialYearService financialYearService) =>
        _financialYearService = financialYearService;

    public void Initialize(FinancialYearListItem item)
    {
        _id = item.Id;
        FinancialYearName = item.Name;
        OpeningDate = item.StartingDate == default ? item.OpeningDate : item.StartingDate;
        OpeningBalanceText = item.StartingBalance.ToString("0.00");
        Notes = item.Notes;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (OpeningDate == default)
        {
            ErrorMessage = "Starting date must be a valid date.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OpeningBalanceText))
        {
            ErrorMessage = "Current bank balance is required.";
            return;
        }

        if (!decimal.TryParse(OpeningBalanceText.Replace("$", "").Replace(",", "").Trim(), out var startingBalance))
        {
            ErrorMessage = "Bank balance must be a valid amount.";
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _financialYearService.UpdateStartingBalanceAsync(
                _id, OpeningDate.Date, startingBalance, Notes);

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
