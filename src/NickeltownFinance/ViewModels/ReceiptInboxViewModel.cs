using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Converters;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels.Dialogs;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class ReceiptInboxViewModel : ViewModelBase
{
    private readonly IReceiptImportService _importService;
    private readonly IReceiptImportQueue _queue;
    private readonly IReceiptMatchingService _matchingService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    private List<ReceiptInboxRowViewModel> _allItems = [];

    [ObservableProperty] private ObservableCollection<ReceiptInboxRowViewModel> _items = [];
    [ObservableProperty] private ReceiptInboxRowViewModel? _selectedItem;
    [ObservableProperty] private ReceiptMatchSuggestionInfo? _selectedMatch;
    [ObservableProperty] private ObservableCollection<ReceiptMatchSuggestionInfo> _matchCandidates = [];
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private string _selectedTab = "All";
    [ObservableProperty] private string _emptyStateMessage = "No receipts waiting.";
    [ObservableProperty] private string _previewStatusText = string.Empty;
    [ObservableProperty] private bool _showPdfPreview;
    [ObservableProperty] private string? _pdfPreviewPath;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _editSupplier = string.Empty;
    [ObservableProperty] private DateTime? _editDate;
    [ObservableProperty] private string _editInvoiceNumber = string.Empty;
    [ObservableProperty] private string _editAbn = string.Empty;
    [ObservableProperty] private decimal? _editSubtotal;
    [ObservableProperty] private decimal? _editGst;
    [ObservableProperty] private decimal? _editTotal;
    [ObservableProperty] private byte? _supplierConfidence;
    [ObservableProperty] private byte? _dateConfidence;
    [ObservableProperty] private byte? _invoiceConfidence;
    [ObservableProperty] private byte? _abnConfidence;
    [ObservableProperty] private byte? _subtotalConfidence;
    [ObservableProperty] private byte? _gstConfidence;
    [ObservableProperty] private byte? _totalConfidence;
    [ObservableProperty] private string _timelineDisplay = string.Empty;
    [ObservableProperty] private string? _duplicateWarning;

    public IReadOnlyList<string> Tabs { get; } =
        ["All", "Processing", "Ready", "Matched", "Needs Review", "Ignored"];

    public ReceiptInboxViewModel(
        IReceiptImportService importService,
        IReceiptImportQueue queue,
        IReceiptMatchingService matchingService,
        INavigationService navigationService,
        INotificationService notificationService,
        IServiceProvider serviceProvider)
    {
        _importService = importService;
        _queue = queue;
        _matchingService = matchingService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;

        _queue.ItemUpdated += OnQueueItemUpdated;
        _ = LoadAsync();
    }

    public void Detach() => _queue.ItemUpdated -= OnQueueItemUpdated;

    private void OnQueueItemUpdated(object? sender, ReceiptImportItemInfo item) =>
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            await LoadAsync(preserveSelectionId: item.Id);
        });

    public async Task LoadAsync(ObjectId? preserveSelectionId = null)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var inbox = await _importService.GetInboxAsync(includeCommitted: true);
            _allItems = inbox.Select(i => new ReceiptInboxRowViewModel(i)).ToList();
            ApplyTabFilter();

            if (preserveSelectionId is { } selectionId && selectionId != ObjectId.Empty)
            {
                SelectedItem = Items.FirstOrDefault(i => i.Source.Id == selectionId)
                    ?? _allItems.FirstOrDefault(i => i.Source.Id == selectionId);
            }
            else if (SelectedItem is not null)
            {
                SelectedItem = Items.FirstOrDefault(i => i.Source.Id == SelectedItem.Source.Id);
            }

            UpdateEmptyState();
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

    partial void OnSelectedTabChanged(string value)
    {
        ApplyTabFilter();
        UpdateEmptyState();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyTabFilter();
        UpdateEmptyState();
    }

    partial void OnSelectedItemChanged(ReceiptInboxRowViewModel? value)
    {
        _ = LoadSelectionDetailsAsync(value);
    }

    private async Task LoadSelectionDetailsAsync(ReceiptInboxRowViewModel? item)
    {
        MatchCandidates.Clear();
        SelectedMatch = null;
        PreviewImage = null;
        ShowPdfPreview = false;
        PdfPreviewPath = null;
        PreviewStatusText = string.Empty;
        DuplicateWarning = null;
        TimelineDisplay = string.Empty;

        if (item is null)
            return;

        PreviewStatusText = BuildStatusLog(item.Source);
        DuplicateWarning = item.Source.IsPossibleDuplicate ? item.Source.DuplicateWarning : null;
        TimelineDisplay = BuildTimelineDisplay(item.Source);
        LoadEditableOcr(item.Source);

        if (item.IsImage && File.Exists(item.PreviewPath))
        {
            try
            {
                PreviewImage = Converters.ImageLoadHelper.LoadUnlocked(item.PreviewPath);
            }
            catch
            {
                PreviewImage = null;
            }
        }
        else if (item.IsPdf)
        {
            ShowPdfPreview = true;
            PdfPreviewPath = item.Source.OriginalFullPath;
        }

        if (item.CanAct)
        {
            var matches = await _matchingService.FindMatchesAsync(item.Source.Id);
            foreach (var match in matches)
                MatchCandidates.Add(match);

            SelectedMatch = item.Source.MatchSuggestion ?? MatchCandidates.FirstOrDefault();
        }
    }

    private void ApplyTabFilter()
    {
        IEnumerable<ReceiptInboxRowViewModel> filtered = SelectedTab switch
        {
            "Processing" => _allItems.Where(i => i.IsProcessing),
            "Ready" => _allItems.Where(i =>
                i.Status == ReceiptImportStatus.Ready
                || i.Status == ReceiptImportStatus.CompletedWithWarnings),
            "Matched" => _allItems.Where(i => i.Status == ReceiptImportStatus.Committed),
            "Needs Review" => _allItems.Where(i =>
                i.Status == ReceiptImportStatus.Failed
                || i.Status == ReceiptImportStatus.CompletedWithWarnings
                || (i.Status == ReceiptImportStatus.Ready && i.Source.MatchSuggestion is null)),
            "Ignored" => _allItems.Where(i => i.Status == ReceiptImportStatus.Ignored),
            _ => _allItems
        };

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(i =>
                i.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || i.CategoryDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)
                || i.TotalDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (i.Source.Ocr?.EffectiveInvoiceNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Source.Ocr?.EffectiveAbn?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Source.Ocr?.FullText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Items = new ObservableCollection<ReceiptInboxRowViewModel>(filtered.OrderByDescending(i => i.Source.CreatedDate));
    }

    private void LoadEditableOcr(ReceiptImportItemInfo item)
    {
        var ocr = item.Ocr;
        EditSupplier = ocr?.EffectiveSupplier ?? string.Empty;
        EditDate = ocr?.EffectiveDate;
        EditInvoiceNumber = ocr?.EffectiveInvoiceNumber ?? string.Empty;
        EditAbn = ocr?.EffectiveAbn ?? string.Empty;
        EditSubtotal = ocr?.EffectiveSubtotal;
        EditGst = ocr?.EffectiveGst;
        EditTotal = ocr?.EffectiveTotal;
        SupplierConfidence = ocr?.SupplierConfidence;
        DateConfidence = ocr?.DateConfidence;
        InvoiceConfidence = ocr?.InvoiceNumberConfidence;
        AbnConfidence = ocr?.AbnConfidence;
        SubtotalConfidence = ocr?.SubtotalConfidence;
        GstConfidence = ocr?.GstConfidence;
        TotalConfidence = ocr?.TotalConfidence;
    }

    private static string BuildTimelineDisplay(ReceiptImportItemInfo item)
    {
        if (item.Timeline.Count == 0)
            return item.StatusDisplay;

        return string.Join(Environment.NewLine, item.Timeline.Select(e =>
        {
            var local = e.TimestampUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            var user = string.IsNullOrWhiteSpace(e.UserName) ? string.Empty : $" · {e.UserName}";
            var duration = e.DurationMs is { } ms ? $" · {ms}ms" : string.Empty;
            return $"{local} · {e.Stage}{user}{duration}";
        }));
    }

    private void UpdateEmptyState()
    {
        EmptyStateMessage = SelectedTab switch
        {
            "Processing" when Items.Count == 0 => "No receipts are processing.",
            "Ready" when Items.Count == 0 => "No receipts ready for review.",
            "Matched" when Items.Count == 0 => "No matched receipts yet.",
            "Needs Review" when Items.Count == 0 => "Nothing needs review right now.",
            "Ignored" when Items.Count == 0 => "No ignored receipts.",
            _ when _allItems.Count == 0 =>
                "No receipts waiting. Upload from your phone or import files from this page.",
            _ => string.Empty
        };
    }

    private static string BuildStatusLog(ReceiptImportItemInfo item)
    {
        var lines = new List<string> { item.StatusDisplay };
        if (!string.IsNullOrWhiteSpace(item.ErrorMessage))
            lines.Add(item.ErrorMessage);
        if (item.Status is ReceiptImportStatus.Queued
            or ReceiptImportStatus.Uploading
            or ReceiptImportStatus.Preprocessing
            or ReceiptImportStatus.ProcessingOcr
            or ReceiptImportStatus.SupplierDetection
            or ReceiptImportStatus.AiParsing
            or ReceiptImportStatus.MatchingTransaction
            or ReceiptImportStatus.GeneratingThumbnail)
            lines.Add("Processing receipt…");
        if (item.Status == ReceiptImportStatus.Ready && item.MatchSuggestion is null && item.Ocr is null)
            lines.Add("Could not read receipt details yet.");
        if (item.Status == ReceiptImportStatus.Ready && item.MatchSuggestion is null && item.Ocr is not null)
            lines.Add("No bank match found.");
        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
            SelectedTab = tab;
    }

    [RelayCommand]
    private void BackToImports() => _navigationService.Navigate<ImportViewModel>();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(SelectedItem?.Source.Id);

    [RelayCommand]
    private void OpenMobileUpload()
    {
        var vm = App.Services.GetRequiredService<MobileReceiptUploadViewModel>();
        var window = new MobileReceiptUploadWindow(vm, new MobileUploadSessionRequest
        {
            ImportTarget = ReceiptImportTarget.Inbox
        })
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task BrowseFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import receipts",
            Filter =
                "Receipt files|*.pdf;*.jpg;*.jpeg;*.png;*.webp;*.heic;*.tif;*.tiff;*.zip|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        await ImportPathsAsync(dialog.FileNames);
    }

    public async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var imported = await _importService.ImportFromDesktopAsync(paths);
            if (imported.Count == 0)
            {
                _notificationService.ShowInfo("No supported receipt files were found.");
                return;
            }

            _notificationService.ShowSuccess($"{imported.Count} receipt(s) added to the inbox.");
            await LoadAsync(imported.Last().Id);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MatchToTransactionAsync()
    {
        if (SelectedItem is null || !SelectedItem.CanAct) return;
        var match = SelectedMatch ?? SelectedItem.Source.MatchSuggestion;
        if (match is null)
        {
            _notificationService.ShowInfo("Select an ANZ transaction match first.");
            return;
        }

        if (match.Confidence < 97 && !AppDialog.Confirm(
                "Match receipt",
                $"Attach this receipt to \"{match.Description}\" ({match.Amount:C})?",
                confirmText: "Match",
                cancelText: "Cancel"))
            return;

        await CommitAsync(new ReceiptImportCommitRequest
        {
            ImportItemId = SelectedItem.Source.Id,
            Action = ReceiptMatchAction.Match,
            TransactionId = match.TransactionId
        });
    }

    [RelayCommand]
    private async Task MatchCandidateAsync(ReceiptMatchSuggestionInfo? match)
    {
        if (SelectedItem is null || match is null || !SelectedItem.CanAct) return;

        await CommitAsync(new ReceiptImportCommitRequest
        {
            ImportItemId = SelectedItem.Source.Id,
            Action = ReceiptMatchAction.Match,
            TransactionId = match.TransactionId
        });
    }

    [RelayCommand]
    private void CreateNewExpense()
    {
        if (SelectedItem is null || !SelectedItem.CanAct) return;

        var editor = _serviceProvider.GetRequiredService<TransactionEditorViewModel>();
        editor.InitializeForReceiptImport(SelectedItem.Source);
        editor.Saved += async (_, _) => await LoadAsync();

        var window = new TransactionEditorWindow(editor)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task IgnoreAsync()
    {
        if (SelectedItem is null || !SelectedItem.CanAct) return;
        if (!AppDialog.Confirm("Ignore receipt", "Ignore this receipt? The file will be kept.", "Ignore", "Cancel"))
            return;

        await CommitAsync(new ReceiptImportCommitRequest
        {
            ImportItemId = SelectedItem.Source.Id,
            Action = ReceiptMatchAction.Ignore
        });
    }

    [RelayCommand]
    private async Task ReprocessAsync()
    {
        if (SelectedItem is null) return;
        try
        {
            await _importService.RetryAsync(SelectedItem.Source.Id);
            _notificationService.ShowInfo("Receipt queued for reprocessing.");
            await LoadAsync(SelectedItem.Source.Id);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem is null) return;
        if (!AppDialog.Confirm("Delete receipt", "Delete this receipt from the inbox?", "Delete", "Cancel"))
            return;

        try
        {
            await _importService.DeleteAsync(SelectedItem.Source.Id);
            SelectedItem = null;
            await LoadAsync();
            _notificationService.ShowInfo("Receipt deleted.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveOcrEditsAsync()
    {
        if (SelectedItem is null) return;

        try
        {
            await _importService.SaveOcrCorrectionsAsync(new ReceiptOcrCorrectionRequest
            {
                ImportItemId = SelectedItem.Source.Id,
                Supplier = string.IsNullOrWhiteSpace(EditSupplier) ? null : EditSupplier.Trim(),
                Date = EditDate,
                InvoiceNumber = string.IsNullOrWhiteSpace(EditInvoiceNumber) ? null : EditInvoiceNumber.Trim(),
                Abn = string.IsNullOrWhiteSpace(EditAbn) ? null : EditAbn.Trim(),
                Subtotal = EditSubtotal,
                Gst = EditGst,
                Total = EditTotal
            });
            await LoadAsync(SelectedItem.Source.Id);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void ViewReceipt()
    {
        if (SelectedItem is null) return;
        var viewer = _serviceProvider.GetRequiredService<ReceiptViewerViewModel>();
        viewer.LoadFromInbox(SelectedItem.Source);
        var window = new Views.Dialogs.ReceiptViewerWindow(viewer)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenOriginalFile()
    {
        if (SelectedItem is null) return;
        var path = SelectedItem.Source.OriginalFullPath;
        if (!File.Exists(path))
        {
            _notificationService.ShowError("Original file not found.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    private async Task CommitAsync(ReceiptImportCommitRequest request)
    {
        IsBusy = true;
        try
        {
            await _importService.CommitAsync(request);
            _notificationService.ShowSuccess(request.Action switch
            {
                ReceiptMatchAction.Match => "Receipt matched to transaction.",
                ReceiptMatchAction.Ignore => "Receipt ignored.",
                ReceiptMatchAction.CreateNew => "Expense created from receipt.",
                _ => "Receipt updated."
            });
            SelectedItem = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
