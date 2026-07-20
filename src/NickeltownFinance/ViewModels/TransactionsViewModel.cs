using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionService _transactionService;
    private readonly IFinancialYearService _financialYearService;
    private readonly ICategoryService _categoryService;
    private readonly IAttachmentService _attachmentService;
    private readonly ISquareDepositService _squareDepositService;
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    private TransactionListItem? _lastDeleted;
    private ObjectId _financialYearId = ObjectId.Empty;

    [ObservableProperty] private ObservableCollection<TransactionListItem> _transactions = [];
    [ObservableProperty] private ObservableCollection<Category> _categories = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Category? _selectedCategoryFilter;
    [ObservableProperty] private TransactionListItem? _selectedTransaction;
    [ObservableProperty] private string? _receiptFilter;
    [ObservableProperty] private AttachmentKind? _attachmentKindFilter;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string? _typeFilter;
    [ObservableProperty] private string _emptyTitle = "No transactions";
    [ObservableProperty] private string _emptyMessage = "Import an ANZ statement or add a transaction.";
    [ObservableProperty] private string _financialYearLabel = string.Empty;
    [ObservableProperty] private System.Windows.Media.ImageSource? _detailThumbnail;
    [ObservableProperty] private string _detailReceiptStatus = string.Empty;
    [ObservableProperty] private int _detailAttachmentCount;
    [ObservableProperty] private bool _showDetailPanel;
    [ObservableProperty] private bool _showSquareDetail;
    [ObservableProperty] private bool _showReceiptDetail;
    [ObservableProperty] private bool _showGeneralDetail;
    [ObservableProperty] private bool _showSquareAwaitingMatch;
    [ObservableProperty] private string _squareDepositDate = string.Empty;
    [ObservableProperty] private decimal _squareGrossSales;
    [ObservableProperty] private decimal _squareFees;
    [ObservableProperty] private decimal _squareNetDeposit;
    [ObservableProperty] private ObservableCollection<SquareDepositGroupViewModel> _squareDepositGroups = [];

    public IReadOnlyList<string> ReceiptFilterOptions { get; } = ["Any", "With Receipts", "Missing Receipts"];
    public IReadOnlyList<string> TypeFilterOptions { get; } = ["All", "Income only", "Expense only"];

    public Array AttachmentKinds => Enum.GetValues(typeof(AttachmentKind));

    public TransactionsViewModel(
        ITransactionService transactionService,
        IFinancialYearService financialYearService,
        ICategoryService categoryService,
        IAttachmentService attachmentService,
        ISquareDepositService squareDepositService,
        INotificationService notificationService,
        IServiceProvider serviceProvider)
    {
        _transactionService = transactionService;
        _financialYearService = financialYearService;
        _categoryService = categoryService;
        _attachmentService = attachmentService;
        _squareDepositService = squareDepositService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        ReceiptFilter = "Any";
        TypeFilter = "All";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var cats = await _categoryService.GetAllActiveAsync();
        Categories = new ObservableCollection<Category>(cats);
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        var fy = _financialYearService.GetActiveYear();

        // If this year has no transactions and the user isn't filtering, show the year that has data.
        if (!HasActiveFilters() && !_financialYearService.HasTransactions(fy.Id))
        {
            var yearWithData = _financialYearService.GetAll()
                .Where(y => _financialYearService.HasTransactions(y.Id))
                .OrderByDescending(y => y.OpeningDate)
                .FirstOrDefault();
            if (yearWithData is not null)
            {
                _financialYearService.SetViewingYear(yearWithData);
                fy = yearWithData;
            }
        }

        _financialYearId = fy.Id;
        FinancialYearLabel = fy.Name;
        IsBusy = true;
        try
        {
            bool? hasReceipt = ReceiptFilter switch
            {
                "With Receipts" => true,
                "Missing Receipts" => false,
                _ => null
            };

            bool? isIncome = TypeFilter switch
            {
                "Income only" => true,
                "Expense only" => false,
                _ => null
            };

            var filter = new TransactionSearchFilter
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                CategoryId = SelectedCategoryFilter?.Id,
                HasReceipt = hasReceipt,
                ReceiptType = AttachmentKindFilter,
                FromDate = FromDate,
                ToDate = ToDate,
                IsIncome = isIncome
            };

            var items = await _transactionService.GetLedgerAsync(_financialYearId, filter: filter);
            Transactions = new ObservableCollection<TransactionListItem>(items);
            UpdateEmptyState(fy.Name, items.Count);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasActiveFilters() =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        SelectedCategoryFilter is not null ||
        (ReceiptFilter is not null && ReceiptFilter != "Any") ||
        (TypeFilter is not null && TypeFilter != "All") ||
        AttachmentKindFilter is not null ||
        FromDate is not null ||
        ToDate is not null;

    private void UpdateEmptyState(string yearName, int count)
    {
        if (count > 0)
            return;

        if (HasActiveFilters())
        {
            EmptyTitle = "No matching transactions";
            EmptyMessage = "No transactions match your filters. Try clearing filters or choose another financial year.";
            return;
        }

        EmptyTitle = "No transactions";
        EmptyMessage = $"No transactions in FY {yearName} yet. Import an ANZ statement or add a transaction.";
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnSelectedCategoryFilterChanged(Category? value) => _ = LoadAsync();
    partial void OnReceiptFilterChanged(string? value) => _ = LoadAsync();
    partial void OnAttachmentKindFilterChanged(AttachmentKind? value) => _ = LoadAsync();
    partial void OnFromDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnTypeFilterChanged(string? value) => _ = LoadAsync();

    partial void OnSelectedTransactionChanged(TransactionListItem? value)
    {
        ShowDetailPanel = value is not null;
        if (value is null)
        {
            ClearDetailPanel();
            return;
        }

        if (value.HasSquareDepositDetail)
        {
            ShowReceiptDetail = false;
            ShowGeneralDetail = false;
            ShowSquareAwaitingMatch = false;
            _ = LoadSquareDepositDetailAsync(value.Id);
            return;
        }

        ShowSquareDetail = false;
        ShowSquareAwaitingMatch = false;

        if (value.ExpenseAmount > 0)
        {
            ShowReceiptDetail = true;
            ShowGeneralDetail = false;
            DetailReceiptStatus = value.ReceiptStatusText;
            DetailAttachmentCount = value.AttachmentCount;
            DetailThumbnail = string.IsNullOrWhiteSpace(value.ThumbnailPath) || !File.Exists(value.ThumbnailPath)
                ? null
                : Converters.ImageLoadHelper.LoadUnlocked(value.ThumbnailPath);
            return;
        }

        ShowReceiptDetail = false;
        ShowGeneralDetail = true;
        DetailReceiptStatus = string.Empty;
        DetailAttachmentCount = 0;
        DetailThumbnail = null;
    }

    private void ClearDetailPanel()
    {
        ShowSquareDetail = false;
        ShowSquareAwaitingMatch = false;
        ShowReceiptDetail = false;
        ShowGeneralDetail = false;
        DetailThumbnail = null;
        DetailReceiptStatus = string.Empty;
        DetailAttachmentCount = 0;
        SquareDepositGroups = [];
        SquareDepositDate = string.Empty;
        SquareGrossSales = 0;
        SquareFees = 0;
        SquareNetDeposit = 0;
    }

    private async Task LoadSquareDepositDetailAsync(ObjectId transactionId)
    {
        ShowSquareAwaitingMatch = false;
        ShowSquareDetail = true;
        ShowReceiptDetail = false;
        ShowGeneralDetail = false;
        DetailThumbnail = null;
        DetailReceiptStatus = string.Empty;
        DetailAttachmentCount = 0;
        SquareDepositGroups = [];

        try
        {
            var detail = await _squareDepositService.GetDetailForBankTransactionAsync(transactionId);
            if (detail is null || SelectedTransaction?.Id != transactionId)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowSquareDetail = false;
                    ShowGeneralDetail = true;
                });
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (SelectedTransaction?.Id != transactionId)
                    return;

                SquareDepositDate = detail.DepositDate.ToString("dd/MM/yyyy");
                SquareGrossSales = detail.GrossSales;
                SquareFees = detail.Fees;
                SquareNetDeposit = detail.NetDeposit;
                SquareDepositGroups = new ObservableCollection<SquareDepositGroupViewModel>(
                    detail.Groups.Select(g => new SquareDepositGroupViewModel(g)));
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load Square deposit details: {ex.Message}";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ShowSquareDetail = false;
                ShowGeneralDetail = true;
            });
        }
    }

    // Square CSV import hidden for now.
    [RelayCommand]
    private void ImportSquareStatement()
    {
        _notificationService.ShowInfo(
            "Square CSV import is turned off. Set this transfer's category, or open it and use Also mark as other categories when needed.");
    }

    [RelayCommand]
    private void ToggleSquareGroup(SquareDepositGroupViewModel? group)
    {
        if (group is null) return;
        group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
        SelectedCategoryFilter = null;
        ReceiptFilter = "Any";
        TypeFilter = "All";
        AttachmentKindFilter = null;
        FromDate = null;
        ToDate = null;
    }

    [RelayCommand]
    private void ImportStatement()
    {
        ImportViewModel.PendingStartupMode = ImportStartupMode.Anz;
        _serviceProvider.GetRequiredService<INavigationService>().Navigate<ImportViewModel>();
    }

    [RelayCommand]
    private void AddTransaction() => OpenEditor(isIncome: false);

    [RelayCommand]
    private void EditTransaction()
    {
        if (SelectedTransaction is null) return;
        var isIncome = SelectedTransaction.IncomeAmount > 0;
        OpenEditor(isIncome, SelectedTransaction.Id);
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync()
    {
        if (SelectedTransaction is null) return;
        if (!AppDialog.Confirm(
                "Delete transaction",
                $"Delete '{SelectedTransaction.Description}'? This can be undone with Ctrl+Z.",
                confirmText: "Delete",
                cancelText: "Cancel",
                isDanger: true))
            return;


        _lastDeleted = SelectedTransaction;
        await _transactionService.SoftDeleteAsync(SelectedTransaction.Id);
        _notificationService.ShowInfo("Transaction deleted. Press Ctrl+Z to undo.");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        if (_lastDeleted is null)
        {
            _notificationService.ShowInfo("Nothing to undo.");
            return;
        }

        await _transactionService.RestoreAsync(_lastDeleted.Id);
        _notificationService.ShowSuccess("Transaction restored.");
        _lastDeleted = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DuplicateTransactionAsync()
    {
        if (SelectedTransaction is null) return;
        await _transactionService.DuplicateAsync(SelectedTransaction.Id);
        _notificationService.ShowSuccess("Transaction duplicated.");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var path = Path.Combine(AppPaths.ExportsPath, $"Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        await _transactionService.ExportLedgerExcelAsync(
            _financialYearId,
            path,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            SelectedCategoryFilter?.Id);
        _notificationService.ShowSuccess($"Exported to {path}");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task PrintAsync() => await ExportAsync();

    [RelayCommand]
    private async Task ViewReceiptsAsync()
    {
        if (SelectedTransaction is null) return;

        var attachments = await _attachmentService.GetForTransactionAsync(SelectedTransaction.Id);
        if (attachments.Count == 0)
        {
            _notificationService.ShowInfo("No receipts or attachments on this transaction.");
            OpenEditor(SelectedTransaction.IncomeAmount > 0, SelectedTransaction.Id);
            return;
        }

        var viewer = _serviceProvider.GetRequiredService<ReceiptViewerViewModel>();
        viewer.Load(attachments);
        var window = new ReceiptViewerWindow(viewer)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private async Task AttachReceiptAsync()
    {
        if (SelectedTransaction is null) return;
        OpenEditor(SelectedTransaction.IncomeAmount > 0, SelectedTransaction.Id);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RemoveReceiptAsync()
    {
        if (SelectedTransaction is null) return;
        var attachments = await _attachmentService.GetForTransactionAsync(SelectedTransaction.Id);
        var receipt = attachments.FirstOrDefault(a => a.Kind == Core.Enums.AttachmentKind.Receipt)
            ?? attachments.FirstOrDefault();
        if (receipt is null)
        {
            _notificationService.ShowInfo("No receipt to remove.");
            return;
        }

        if (!AppDialog.Confirm("Remove receipt", $"Remove {receipt.FileName} from this transaction?", "Remove", "Cancel", isDanger: true))
            return;

        await _attachmentService.DeleteAsync(receipt.Id);
        _notificationService.ShowSuccess("Receipt removed.");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ReplaceReceiptAsync()
    {
        if (SelectedTransaction is null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Receipt files|*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var attachments = await _attachmentService.GetForTransactionAsync(SelectedTransaction.Id);
        foreach (var existing in attachments.Where(a => a.Kind == Core.Enums.AttachmentKind.Receipt))
            await _attachmentService.DeleteAsync(existing.Id);

        await _attachmentService.AddAsync(
            SelectedTransaction.Id,
            dialog.FileName,
            Core.Enums.AttachmentKind.Receipt);
        _notificationService.ShowSuccess("Receipt replaced.");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddAnotherReceiptAsync()
    {
        if (SelectedTransaction is null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Receipt files|*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.tif;*.tiff|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
            await _attachmentService.AddAsync(SelectedTransaction.Id, file, Core.Enums.AttachmentKind.Receipt);

        _notificationService.ShowSuccess("Receipt(s) added.");
        await LoadAsync();
    }

    public async Task DropFilesOnTransactionAsync(TransactionListItem item, IEnumerable<string> files)
    {
        SelectedTransaction = item;
        foreach (var file in files)
        {
            try
            {
                var kind = item.ExpenseAmount > 0 ? AttachmentKind.Receipt : AttachmentKind.Other;
                await _attachmentService.AddAsync(item.Id, file, kind);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError(ex.Message);
            }
        }

        _notificationService.ShowSuccess("Attachment(s) added.");
        await LoadAsync();
    }

    public void OpenReceiptViewerForSelected()
    {
        if (SelectedTransaction is null) return;
        _ = ViewReceiptsAsync();
    }

    public void EditSelectedTransaction()
    {
        if (SelectedTransaction is null) return;
        var isIncome = SelectedTransaction.IncomeAmount > 0;
        OpenEditor(isIncome, SelectedTransaction.Id);
    }

    private void OpenEditor(bool isIncome, ObjectId? transactionId = null)
    {
        var editor = _serviceProvider.GetRequiredService<TransactionEditorViewModel>();
        editor.Initialize(isIncome, transactionId);
        editor.Saved += async (_, _) => await LoadAsync();
        var window = new TransactionEditorWindow(editor)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }
}
