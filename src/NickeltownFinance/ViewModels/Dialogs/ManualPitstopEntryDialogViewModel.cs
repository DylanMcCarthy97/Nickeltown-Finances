using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class ManualPitstopEntryDialogViewModel : ViewModelBase
{
    private readonly IMonthDocumentService _monthDocumentService;
    private ObjectId _id = ObjectId.Empty;
    private int _year;
    private int _month;
    private bool _syncingTitle;

    [ObservableProperty] private string _dialogTitle = "Manual Pitstop entry";
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private DateTime _eventDate = DateTime.Today;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _cashAmountText = string.Empty;
    [ObservableProperty] private string _cardAmountText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _totalDisplay = "$0.00";

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public ManualPitstopEntryDialogViewModel(IMonthDocumentService monthDocumentService) =>
        _monthDocumentService = monthDocumentService;

    public void Initialize(int year, int month, MonthDocumentInfo? existing = null)
    {
        _year = year;
        _month = month;
        MonthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");
        ErrorMessage = null;
        DialogResult = false;

        if (existing is null)
        {
            _id = ObjectId.Empty;
            DialogTitle = "Manual Pitstop entry";
            EventDate = DefaultEventDate(year, month);
            _syncingTitle = true;
            Title = AutoTitle(EventDate);
            _syncingTitle = false;
            CashAmountText = string.Empty;
            CardAmountText = string.Empty;
            Notes = string.Empty;
        }
        else
        {
            _id = existing.Id;
            DialogTitle = "Edit manual Pitstop entry";
            EventDate = existing.EventDate?.Date ?? DefaultEventDate(year, month);
            Title = string.IsNullOrWhiteSpace(existing.Title) ? AutoTitle(EventDate) : existing.Title;
            CashAmountText = existing.CashAmount == 0 ? string.Empty : existing.CashAmount.ToString("0.00");
            CardAmountText = existing.CardAmount == 0 ? string.Empty : existing.CardAmount.ToString("0.00");
            Notes = existing.Notes ?? string.Empty;
        }

        UpdateTotalDisplay();
    }

    partial void OnEventDateChanged(DateTime oldValue, DateTime newValue)
    {
        if (_syncingTitle)
            return;

        if (string.IsNullOrWhiteSpace(Title) || IsAutoTitle(Title, oldValue))
        {
            _syncingTitle = true;
            Title = AutoTitle(newValue);
            _syncingTitle = false;
        }
    }

    partial void OnCashAmountTextChanged(string value) => UpdateTotalDisplay();

    partial void OnCardAmountTextChanged(string value) => UpdateTotalDisplay();

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (EventDate == default)
        {
            ErrorMessage = "Event date is required.";
            return;
        }

        if (!TryParseAmount(CashAmountText, out var cash))
        {
            ErrorMessage = "Cash amount must be a valid number.";
            return;
        }

        if (!TryParseAmount(CardAmountText, out var card))
        {
            ErrorMessage = "Card amount must be a valid number.";
            return;
        }

        if (cash < 0 || card < 0)
        {
            ErrorMessage = "Cash and card amounts cannot be negative.";
            return;
        }

        if (cash == 0 && card == 0)
        {
            ErrorMessage = "Enter a cash amount, a card amount, or both.";
            return;
        }

        if (EventDate.Year != _year || EventDate.Month != _month)
        {
            ErrorMessage = $"Event date must be in {MonthLabel}.";
            return;
        }

        IsBusy = true;
        try
        {
            if (_id == ObjectId.Empty)
            {
                await _monthDocumentService.AddManualPitstopAsync(
                    _year, _month, EventDate.Date, cash, card, Title, Notes);
            }
            else
            {
                await _monthDocumentService.UpdateManualPitstopAsync(
                    _id, EventDate.Date, cash, card, Title, Notes);
            }

            DialogResult = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
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

    private void UpdateTotalDisplay()
    {
        var cash = TryParseAmount(CashAmountText, out var cashValue) ? cashValue : 0m;
        var card = TryParseAmount(CardAmountText, out var cardValue) ? cardValue : 0m;
        TotalDisplay = (cash + card).ToString("C", CultureInfo.CurrentCulture);
    }

    private static DateTime DefaultEventDate(int year, int month)
    {
        var today = DateTime.Today;
        if (today.Year == year && today.Month == month)
            return today;
        return new DateTime(year, month, 1);
    }

    private static string AutoTitle(DateTime date) =>
        $"Pitstop {date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

    private static bool IsAutoTitle(string title, DateTime currentDate)
    {
        var trimmed = title.Trim();
        if (trimmed == AutoTitle(currentDate))
            return true;

        return trimmed.StartsWith("Pitstop ", StringComparison.Ordinal)
               && DateTime.TryParseExact(
                   trimmed["Pitstop ".Length..],
                   "d MMM yyyy",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out _);
    }

    private static bool TryParseAmount(string? text, out decimal value)
    {
        var cleaned = (text ?? string.Empty).Replace("$", "").Replace(",", "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            value = 0m;
            return true;
        }

        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
            return true;
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
