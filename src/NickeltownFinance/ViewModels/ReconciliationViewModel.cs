using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Services;

namespace NickeltownFinance.ViewModels;

public partial class ReconciliationViewModel : ViewModelBase
{
    private readonly IReconciliationService _reconciliationService;
    private readonly INotificationService _notificationService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private int _bankDeposits;
    [ObservableProperty] private int _matched;
    [ObservableProperty] private int _needsReview;
    [ObservableProperty] private int _unmatchedAnz;
    [ObservableProperty] private int _unmatchedSquare;
    [ObservableProperty] private int _duplicateTransactions;
    [ObservableProperty] private int _importErrors;
    [ObservableProperty] private decimal _reconciledTotal;

    [ObservableProperty] private ObservableCollection<ReconciliationMatchItem> _matchedDeposits = [];
    [ObservableProperty] private ObservableCollection<ReconciliationAnzItem> _unmatchedAnzDeposits = [];
    [ObservableProperty] private ObservableCollection<ReconciliationSquareItem> _unmatchedSquareDeposits = [];
    [ObservableProperty] private ObservableCollection<ReconciliationMatchCandidate> _manualReviewItems = [];

    [ObservableProperty] private ReconciliationMatchCandidate? _selectedReviewItem;
    [ObservableProperty] private ReconciliationSquareItem? _selectedSquareCandidate;

    public ReconciliationViewModel(
        IReconciliationService reconciliationService,
        INotificationService notificationService,
        INavigationService navigationService)
    {
        _reconciliationService = reconciliationService;
        _notificationService = notificationService;
        _navigationService = navigationService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summary = await _reconciliationService.GetSummaryAsync();
            BankDeposits = summary.BankDeposits;
            Matched = summary.Matched;
            NeedsReview = summary.NeedsReview;
            UnmatchedAnz = summary.UnmatchedAnz;
            UnmatchedSquare = summary.UnmatchedSquare;
            DuplicateTransactions = summary.DuplicateTransactions;
            ImportErrors = summary.ImportErrors;
            ReconciledTotal = summary.ReconciledTotal;
            MatchedDeposits = new ObservableCollection<ReconciliationMatchItem>(summary.MatchedDeposits);
            UnmatchedAnzDeposits = new ObservableCollection<ReconciliationAnzItem>(summary.UnmatchedAnzDeposits);
            UnmatchedSquareDeposits = new ObservableCollection<ReconciliationSquareItem>(summary.UnmatchedSquareDeposits);
            ManualReviewItems = new ObservableCollection<ReconciliationMatchCandidate>(summary.ManualReviewItems);
            SelectedReviewItem = ManualReviewItems.FirstOrDefault();
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

    partial void OnSelectedReviewItemChanged(ReconciliationMatchCandidate? value)
    {
        SelectedSquareCandidate = value?.PossibleSquareDeposits.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task ConfirmMatchAsync()
    {
        if (SelectedReviewItem is null || SelectedSquareCandidate is null)
        {
            _notificationService.ShowInfo("Select a Square deposit to match.");
            return;
        }

        var (success, error) = await _reconciliationService.ManualMatchAsync(
            SelectedReviewItem.AnzDeposit.TransactionId,
            SelectedSquareCandidate.Id);

        if (!success)
        {
            AppDialog.Error("Unable to match", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess("Square deposit matched.");
        await LoadAsync();
    }

    [RelayCommand]
    private void BackToImports() => _navigationService.Navigate<ImportViewModel>();
}
