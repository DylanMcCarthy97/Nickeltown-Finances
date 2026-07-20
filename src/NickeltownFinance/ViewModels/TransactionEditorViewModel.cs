using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels.Dialogs;
using NickeltownFinance.Views.Dialogs;
using TransactionModel = NickeltownFinance.Core.Models.Transaction;

namespace NickeltownFinance.ViewModels;

public partial class TransactionEditorViewModel : ViewModelBase
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryService _categoryService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAttachmentService _attachmentService;
    private readonly ICategorisationService _categorisationService;
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReceiptImportService _receiptImportService;
    private readonly IReceiptImportQueue _receiptImportQueue;
    private readonly IMobileUploadHost _mobileUploadHost;

    [ObservableProperty] private bool _isIncome;
    [ObservableProperty] private string _title = "New Transaction";
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.EFT;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private bool _alwaysUseCategoryForDescription = true;
    [ObservableProperty] private ObservableCollection<Category> _categories = [];
    [ObservableProperty] private ObservableCollection<CategorySplitLineViewModel> _categoryTags = [];
    [ObservableProperty] private bool _isSquareDeposit;
    [ObservableProperty] private bool _markAdditionalCategories;
    [ObservableProperty] private ObservableCollection<AttachmentInfo> _attachments = [];
    [ObservableProperty] private AttachmentInfo? _selectedAttachment;
    [ObservableProperty] private AttachmentKind _selectedAttachmentKind = AttachmentKind.Receipt;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private double _previewZoom = 1.0;
    [ObservableProperty] private double _previewRotation;
    [ObservableProperty] private string _receiptStatusBadge = string.Empty;
    [ObservableProperty] private string _receiptStatusDetail = string.Empty;
    [ObservableProperty] private bool _hasReceiptPreview;
    [ObservableProperty] private bool _isMobileUploadActive;
    [ObservableProperty] private string _mobileUploadStatus = string.Empty;
    [ObservableProperty] private bool _canRetryReceiptProcessing;

    public bool HasPendingMobileUpload => _trackedImportIds.Count > 0;

    public bool ShowCategoryTags => true;

    public Array PaymentMethods => Enum.GetValues(typeof(PaymentMethod));
    public Array AttachmentKinds => Enum.GetValues(typeof(AttachmentKind));

    public event EventHandler? Saved;
    public event EventHandler? RequestClose;

    private ObjectId _editId = ObjectId.Empty;
    private ObjectId? _originalCategoryId;
    private ObjectId _receiptImportItemId = ObjectId.Empty;
    private readonly string _mobileSessionKey = Guid.NewGuid().ToString("N");
    private readonly HashSet<ObjectId> _trackedImportIds = [];
    private ObjectId _activeImportItemId = ObjectId.Empty;
    private ObjectId _ocrAppliedForImportId = ObjectId.Empty;

    public TransactionEditorViewModel(
        ITransactionService transactionService,
        ICategoryService categoryService,
        ITransactionRepository transactionRepository,
        IAttachmentService attachmentService,
        ICategorisationService categorisationService,
        INotificationService notificationService,
        IServiceProvider serviceProvider,
        IReceiptImportService receiptImportService,
        IReceiptImportQueue receiptImportQueue,
        IMobileUploadHost mobileUploadHost)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _transactionRepository = transactionRepository;
        _attachmentService = attachmentService;
        _categorisationService = categorisationService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        _receiptImportService = receiptImportService;
        _receiptImportQueue = receiptImportQueue;
        _mobileUploadHost = mobileUploadHost;
        _receiptImportQueue.ItemUpdated += OnReceiptImportItemUpdated;
    }

    public async void InitializeForReceiptImport(ReceiptImportItemInfo item)
    {
        try
        {
            _receiptImportItemId = item.Id;
            _editId = ObjectId.Empty;
            IsIncome = false;
            Title = "New expense from receipt";
            Date = item.Ocr?.EffectiveDate ?? DateTime.Today;
            Description = item.Ocr?.EffectiveSupplier ?? Path.GetFileNameWithoutExtension(item.FileName);
            Amount = item.Ocr?.EffectiveTotal ?? 0;
            Reference = item.Ocr?.EffectiveInvoiceNumber ?? string.Empty;
            Notes = item.Ocr?.EffectiveGst is { } gst
                ? $"GST {gst:C}. Imported from {item.Source} receipt."
                : $"Imported from {item.Source} receipt.";
            SelectedPaymentMethod = PaymentMethod.EFT;

            await LoadCategoriesAsync();
            if (item.AiSuggestion?.CategoryId is { } categoryId && categoryId != ObjectId.Empty)
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notificationService.ShowError(ex.Message);
        }
    }

    public async void Initialize(bool isIncome, ObjectId? transactionId = null)
    {
        try
        {
            _receiptImportItemId = ObjectId.Empty;
            _editId = transactionId ?? ObjectId.Empty;
            IsIncome = isIncome;
            UpdateTitle();
            await LoadCategoriesAsync();

            if (_editId != ObjectId.Empty)
            {
                var txn = _transactionRepository.GetById(_editId);
                if (txn is null)
                {
                    ErrorMessage = "This transaction no longer exists.";
                    _notificationService.ShowInfo("This transaction no longer exists.");
                    RequestClose?.Invoke(this, EventArgs.Empty);
                    return;
                }

                Date = txn.Date;
                Description = txn.Description;
                IsIncome = txn.IncomeAmount > 0;
                await LoadCategoriesAsync();
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == txn.CategoryId);
                _originalCategoryId = txn.CategoryId;
                Amount = IsIncome ? txn.IncomeAmount : txn.ExpenseAmount;
                SelectedPaymentMethod = txn.PaymentMethod;
                Reference = txn.Reference;
                Notes = txn.Notes;
                IsSquareDeposit = txn.IsSquareDeposit;
                LoadCategoryTags(txn);
                UpdateTitle();
                await ReloadAttachmentsAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notificationService.ShowError(ex.Message);
        }
    }

    partial void OnIsIncomeChanged(bool value)
    {
        if (_editId == ObjectId.Empty || Categories.Count == 0 || SelectedCategory is null
            || (value && SelectedCategory.Type != CategoryType.Income)
            || (!value && SelectedCategory.Type != CategoryType.Expense))
        {
            _ = ReloadCategoriesForTypeAsync();
        }

        UpdateTitle();
        OnPropertyChanged(nameof(ShowCategoryTags));
    }

    private async Task ReloadCategoriesForTypeAsync()
    {
        var previousId = SelectedCategory?.Id;
        await LoadCategoriesAsync();
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == previousId) ?? Categories.FirstOrDefault();
    }

    private async Task LoadCategoriesAsync()
    {
        var type = IsIncome ? CategoryType.Income : CategoryType.Expense;
        var cats = await _categoryService.GetByTypeAsync(type);
        Categories = new ObservableCollection<Category>(cats);
        SelectedCategory ??= Categories.FirstOrDefault();
        RefreshTagCategoryOptions();
    }

    private void UpdateTitle()
    {
        Title = _editId != ObjectId.Empty
            ? (IsIncome ? "Edit income" : "Edit expense")
            : (IsIncome ? "New income" : "New expense");
    }

    private void LoadCategoryTags(TransactionModel txn)
    {
        DetachTagLineHandlers();
        CategoryTags.Clear();

        var extras = TransactionCategoryHelper.GetExtraCategoryIds(txn).ToList();
        if (extras.Count > 0)
        {
            foreach (var id in extras)
            {
                var line = new CategorySplitLineViewModel(Categories.FirstOrDefault(c => c.Id == id));
                AttachTagLineHandler(line);
                CategoryTags.Add(line);
            }

            MarkAdditionalCategories = true;
        }
        else
        {
            MarkAdditionalCategories = false;
        }

        OnPropertyChanged(nameof(ShowCategoryTags));
    }

    partial void OnMarkAdditionalCategoriesChanged(bool value)
    {
        if (value && CategoryTags.Count == 0)
        {
            var line = new CategorySplitLineViewModel(null);
            AttachTagLineHandler(line);
            CategoryTags.Add(line);
        }
    }

    private void AttachTagLineHandler(CategorySplitLineViewModel line)
    {
        line.PropertyChanged += OnTagLinePropertyChanged;
    }

    private void DetachTagLineHandlers()
    {
        foreach (var line in CategoryTags)
            line.PropertyChanged -= OnTagLinePropertyChanged;
    }

    private void OnTagLinePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
    }

    private void RefreshTagCategoryOptions()
    {
        foreach (var line in CategoryTags)
        {
            if (line.SelectedCategory is null)
                continue;
            line.SelectedCategory = Categories.FirstOrDefault(c => c.Id == line.SelectedCategory.Id)
                                    ?? line.SelectedCategory;
        }
    }

    [RelayCommand]
    private void AddCategoryTagLine()
    {
        var line = new CategorySplitLineViewModel(null);
        AttachTagLineHandler(line);
        CategoryTags.Add(line);
    }

    [RelayCommand]
    private void RemoveCategoryTagLine(CategorySplitLineViewModel? line)
    {
        if (line is null)
            return;

        line.PropertyChanged -= OnTagLinePropertyChanged;
        CategoryTags.Remove(line);
        if (CategoryTags.Count == 0)
            MarkAdditionalCategories = false;
    }

    [RelayCommand]
    private void SetIncome() => IsIncome = true;

    [RelayCommand]
    private void SetExpense() => IsIncome = false;

    private async Task ReloadAttachmentsAsync()
    {
        if (_editId == ObjectId.Empty)
        {
            Attachments = [];
            return;
        }

        Attachments = new ObservableCollection<AttachmentInfo>(
            await _attachmentService.GetForTransactionAsync(_editId));
        UpdatePreviewFromAttachments();
    }

    partial void OnSelectedAttachmentChanged(AttachmentInfo? value) => UpdatePreviewFromAttachments();

    private void UpdatePreviewFromAttachments()
    {
        var attachment = SelectedAttachment ?? Attachments.FirstOrDefault();
        if (attachment is null || !attachment.IsImage || !File.Exists(attachment.FullPath))
        {
            if (_activeImportItemId == ObjectId.Empty)
            {
                PreviewImage = null;
                HasReceiptPreview = false;
                ReceiptStatusBadge = string.Empty;
                ReceiptStatusDetail = string.Empty;
            }

            return;
        }

        PreviewImage = LoadBitmap(attachment.DisplayPreviewPath ?? attachment.FullPath);
        HasReceiptPreview = true;
        ReceiptStatusBadge = "Ready";
        ReceiptStatusDetail = attachment.FileName;
        PreviewZoom = 1.0;
        PreviewRotation = 0;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private async void OnReceiptImportItemUpdated(object? sender, ReceiptImportItemInfo item)
    {
        if (!string.Equals(item.UploadSessionKey, _mobileSessionKey, StringComparison.Ordinal))
            return;

        _trackedImportIds.Add(item.Id);
        _activeImportItemId = item.Id;
        OnPropertyChanged(nameof(HasPendingMobileUpload));

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            MobileUploadStatus = item.StatusDisplay;
            CanRetryReceiptProcessing = item.Status == ReceiptImportStatus.Failed;
            UpdateImportPreview(item);

            if (item.Status is ReceiptImportStatus.Committed)
            {
                ReceiptStatusBadge = item.AttachedDespiteProcessingFailure
                    ? "Attached"
                    : "Attached";
                ReceiptStatusDetail = item.StatusDisplay;
                if (_editId != ObjectId.Empty)
                    await ReloadAttachmentsAsync();
                return;
            }

            if (item.Status is ReceiptImportStatus.Ready
                or ReceiptImportStatus.CompletedWithWarnings
                or ReceiptImportStatus.Failed)
            {
                TryApplyImportOcr(item);
                if (_editId != ObjectId.Empty)
                    await ReloadAttachmentsAsync();
                else
                    MobileUploadStatus = "Receipt processed. Save the expense to attach it.";
            }
        });
    }

    private void TryApplyImportOcr(ReceiptImportItemInfo item)
    {
        if (_ocrAppliedForImportId == item.Id)
            return;

        if (item.Ocr is null)
            return;

        if (item.Status is not (ReceiptImportStatus.Ready or ReceiptImportStatus.CompletedWithWarnings))
            return;

        _ocrAppliedForImportId = item.Id;
        var ocr = item.Ocr;

        if (string.IsNullOrWhiteSpace(Description))
            Description = ocr.EffectiveSupplier ?? Path.GetFileNameWithoutExtension(item.FileName);

        if (Amount <= 0 && ocr.EffectiveTotal is { } total)
            Amount = total;

        if (ocr.EffectiveDate is { } ocrDate)
            Date = ocrDate;

        if (string.IsNullOrWhiteSpace(Reference) && !string.IsNullOrWhiteSpace(ocr.EffectiveInvoiceNumber))
            Reference = ocr.EffectiveInvoiceNumber;

        if (string.IsNullOrWhiteSpace(Notes) && ocr.EffectiveGst is { } gst)
            Notes = $"GST {gst:C}. Imported from {item.Source} receipt.";

        if (item.AiSuggestion?.CategoryId is { } categoryId && categoryId != ObjectId.Empty)
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId);
    }

    private void UpdateImportPreview(ReceiptImportItemInfo item)
    {
        ReceiptStatusBadge = item.Status switch
        {
            ReceiptImportStatus.Queued => "Uploaded",
            ReceiptImportStatus.Preprocessing or ReceiptImportStatus.ProcessingOcr
                or ReceiptImportStatus.SupplierDetection or ReceiptImportStatus.AiParsing
                or ReceiptImportStatus.MatchingTransaction or ReceiptImportStatus.GeneratingThumbnail => "Processing",
            ReceiptImportStatus.Ready => "Ready",
            ReceiptImportStatus.CompletedWithWarnings => "Ready",
            ReceiptImportStatus.Failed => "Processing failed",
            ReceiptImportStatus.Committed => item.AttachedDespiteProcessingFailure ? "Attached" : "Attached",
            _ => item.StatusDisplay
        };

        if (item.IsPossibleDuplicate && !string.IsNullOrWhiteSpace(item.DuplicateWarning))
            ReceiptStatusDetail = item.DuplicateWarning;
        else if (item.AttachedDespiteProcessingFailure)
            ReceiptStatusDetail = "Processing failed, original saved.";
        else if (item.Status is ReceiptImportStatus.Ready or ReceiptImportStatus.CompletedWithWarnings
                 && item.Ocr is null)
            ReceiptStatusDetail = "No receipt data found — check the image shows a clear receipt.";
        else
            ReceiptStatusDetail = item.ErrorMessage ?? item.StatusDisplay;

        MobileUploadStatus = item.StatusDisplay;

        var previewPath = item.PreviewFullPaths.FirstOrDefault()
            ?? item.ProcessedFullPath
            ?? item.ThumbnailFullPath
            ?? item.OriginalFullPath;
        if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
        {
            PreviewImage = LoadBitmap(previewPath);
            HasReceiptPreview = true;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!await PersistAsync(closeAfterSave: true))
            return;
    }

    private async Task<bool> PersistAsync(bool closeAfterSave)
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Description is required.";
            return false;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = "Please select a category.";
            return false;
        }

        if (Amount <= 0)
        {
            ErrorMessage = "Amount must be greater than zero.";
            return false;
        }

        if (Date == default)
        {
            ErrorMessage = "Please enter a valid date.";
            return false;
        }

        List<CategoryAllocation> extraTags = [];
        if (MarkAdditionalCategories)
        {
            var primaryId = SelectedCategory.Id;
            var tagIds = CategoryTags
                .Select(x => x.SelectedCategory?.Id ?? ObjectId.Empty)
                .Where(id => id != ObjectId.Empty && id != primaryId)
                .Distinct()
                .ToList();

            if (tagIds.Count == 0)
            {
                ErrorMessage = "Pick at least one extra category to mark, or turn off Also mark as.";
                return false;
            }

            extraTags = tagIds.Select(id => new CategoryAllocation { CategoryId = id }).ToList();
        }

        var txn = _editId != ObjectId.Empty
            ? _transactionRepository.GetById(_editId) ?? new TransactionModel()
            : new TransactionModel();

        txn.Date = Date;
        txn.Description = (Description ?? string.Empty).Trim();
        txn.CategoryId = SelectedCategory.Id;
        txn.CategoryAllocations = extraTags;
        txn.PaymentMethod = SelectedPaymentMethod;
        txn.Reference = (Reference ?? string.Empty).Trim();
        txn.Notes = (Notes ?? string.Empty).Trim();
        if (IsSquareDeposit)
            txn.IsSquareDeposit = true;

        if (IsIncome)
        {
            txn.IncomeAmount = Amount;
            txn.ExpenseAmount = 0;
        }
        else
        {
            txn.ExpenseAmount = Amount;
            txn.IncomeAmount = 0;
        }

        try
        {
            if (_receiptImportItemId != ObjectId.Empty)
            {
                if (SelectedCategory is null)
                {
                    ErrorMessage = "Please select a category.";
                    return false;
                }

                await _receiptImportService.CommitAsync(new ReceiptImportCommitRequest
                {
                    ImportItemId = _receiptImportItemId,
                    Action = ReceiptMatchAction.CreateNew,
                    CategoryId = SelectedCategory.Id,
                    Date = Date,
                    Amount = Amount,
                    Description = (Description ?? string.Empty).Trim(),
                    AttachmentKind = AttachmentKind.Receipt
                });

                _receiptImportItemId = ObjectId.Empty;
                Saved?.Invoke(this, EventArgs.Empty);

                if (closeAfterSave)
                {
                    _notificationService.ShowSuccess("Expense created from receipt.");
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }

                return true;
            }

            await _transactionService.SaveAsync(txn, IsIncome);
            _editId = txn.Id;
            Title = IsIncome ? "Edit Income" : "Edit Expense";

            await _receiptImportService.AttachSessionItemsToTransactionAsync(_mobileSessionKey, _editId);
            await ReloadAttachmentsAsync();
            ClearMobileUploadState();

            var rememberCategoryId = SelectedCategory.Id;
            if (AlwaysUseCategoryForDescription &&
                rememberCategoryId != ObjectId.Empty &&
                (_originalCategoryId is null || _originalCategoryId != rememberCategoryId))
                await _categorisationService.RememberAsync(txn.Description, rememberCategoryId);

            _originalCategoryId = rememberCategoryId;
            Saved?.Invoke(this, EventArgs.Empty);

            if (closeAfterSave)
            {
                _notificationService.ShowSuccess("Transaction saved.");
                RequestClose?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task DiscardMobileUploadAsync()
    {
        if (!HasPendingMobileUpload)
            return;

        if (!AppDialog.Confirm(
                "Discard upload?",
                "Remove the uploaded receipt from this expense?",
                confirmText: "Discard",
                cancelText: "Cancel",
                isDanger: true))
            return;

        try
        {
            await _receiptImportService.ResolveSessionUploadsOnCancelAsync(_mobileSessionKey, keepInInbox: false);
            ClearMobileUploadState();
            _notificationService.ShowSuccess("Upload discarded.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    private void ClearMobileUploadState()
    {
        _trackedImportIds.Clear();
        _activeImportItemId = ObjectId.Empty;
        _ocrAppliedForImportId = ObjectId.Empty;
        PreviewImage = null;
        HasReceiptPreview = false;
        ReceiptStatusBadge = string.Empty;
        ReceiptStatusDetail = string.Empty;
        IsMobileUploadActive = false;
        MobileUploadStatus = string.Empty;
        CanRetryReceiptProcessing = false;
        OnPropertyChanged(nameof(HasPendingMobileUpload));
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (_trackedImportIds.Count > 0 && _editId == ObjectId.Empty)
        {
            var keepInInbox = AppDialog.Confirm(
                "Keep uploaded receipt?",
                "You uploaded a receipt for this expense. Keep it in the Receipt Inbox?",
                confirmText: "Keep in inbox",
                cancelText: "Discard");

            await _receiptImportService.ResolveSessionUploadsOnCancelAsync(_mobileSessionKey, keepInInbox);
            ClearMobileUploadState();
        }

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task MobileUploadAsync()
    {
        try
        {
            var vm = _serviceProvider.GetRequiredService<MobileReceiptUploadViewModel>();
            var sessionRequest = new MobileUploadSessionRequest
            {
                SessionKey = _mobileSessionKey,
                TargetTransactionId = _editId != ObjectId.Empty ? _editId : null,
                ImportTarget = ReceiptImportTarget.Transaction
            };

            IsMobileUploadActive = true;
            MobileUploadStatus = "Waiting for upload…";

            var window = new MobileReceiptUploadWindow(vm, sessionRequest)
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            IsMobileUploadActive = _mobileUploadHost.IsRunning;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RetryReceiptProcessingAsync()
    {
        if (_activeImportItemId == ObjectId.Empty) return;

        try
        {
            await _receiptImportService.RetryAsync(_activeImportItemId);
            CanRetryReceiptProcessing = false;
            MobileUploadStatus = "Retrying processing…";
            ReceiptStatusBadge = "Processing";
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void PreviewZoomIn() => PreviewZoom = Math.Min(PreviewZoom + 0.25, 5);

    [RelayCommand]
    private void PreviewZoomOut() => PreviewZoom = Math.Max(PreviewZoom - 0.25, 0.25);

    [RelayCommand]
    private void PreviewFitWidth() => PreviewZoom = 1.0;

    [RelayCommand]
    private void PreviewFitPage() => PreviewZoom = 0.85;

    [RelayCommand]
    private void PreviewRotate() => PreviewRotation = (PreviewRotation + 90) % 360;

    [RelayCommand]
    private void PreviewPrevious()
    {
        if (Attachments.Count == 0) return;
        var index = SelectedAttachment is null
            ? 0
            : Attachments.ToList().FindIndex(a => a.Id == SelectedAttachment.Id);
        if (index > 0)
            SelectedAttachment = Attachments[index - 1];
    }

    [RelayCommand]
    private void PreviewNext()
    {
        if (Attachments.Count == 0) return;
        var index = SelectedAttachment is null
            ? 0
            : Attachments.ToList().FindIndex(a => a.Id == SelectedAttachment.Id);
        if (index >= 0 && index < Attachments.Count - 1)
            SelectedAttachment = Attachments[index + 1];
    }

    [RelayCommand]
    private void OpenPreviewOriginal()
    {
        var path = SelectedAttachment?.FullPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void DownloadPreview()
    {
        var attachment = SelectedAttachment ?? Attachments.FirstOrDefault();
        if (attachment is null || !File.Exists(attachment.FullPath)) return;

        var dialog = new SaveFileDialog
        {
            FileName = attachment.FileName,
            Filter = "All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        File.Copy(attachment.FullPath, dialog.FileName, overwrite: true);
    }

    [RelayCommand]
    private async Task PasteAttachmentAsync()
    {
        if (Clipboard.ContainsFileDropList())
        {
            foreach (var file in Clipboard.GetFileDropList())
            {
                if (!string.IsNullOrWhiteSpace(file))
                    await AddFileAsync(file);
            }

            return;
        }

        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is null) return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            await AddBytesAsync(ms.ToArray(), $"paste_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }
    }

    [RelayCommand]
    private async Task BrowseAttachmentAsync()
    {
        if (!await EnsureTransactionSavedAsync()) return;

        var extensions = string.Join(";", _attachmentService.SupportedExtensions.Select(e => $"*{e}"));
        var dialog = new OpenFileDialog
        {
            Title = "Add attachment",
            Filter = $"Supported files|{extensions}|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
            await AddFileAsync(file);
    }

    public async Task AddFileAsync(string filePath)
    {
        if (!await EnsureTransactionSavedAsync()) return;

        try
        {
            if (Attachments.Any(a => a.Kind == AttachmentKind.Receipt))
            {
                var replace = AppDialog.Confirm(
                    "Replace receipt?",
                    "This transaction already has a receipt. Replace it?",
                    confirmText: "Replace",
                    cancelText: "Keep both");
                if (replace)
                {
                    foreach (var existing in Attachments.Where(a => a.Kind == AttachmentKind.Receipt).ToList())
                        await _attachmentService.DeleteAsync(existing.Id);
                }
            }

            var kind = ResolveAttachmentKind();
            await _attachmentService.AddAsync(_editId, filePath, kind);
            await ReloadAttachmentsAsync();
            _notificationService.ShowSuccess("Attachment added.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    public async Task AddBytesAsync(byte[] data, string fileName)
    {
        if (!await EnsureTransactionSavedAsync()) return;

        try
        {
            await _attachmentService.AddFromBytesAsync(_editId, data, fileName, ResolveAttachmentKind());
            await ReloadAttachmentsAsync();
            _notificationService.ShowSuccess("Attachment added.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    private AttachmentKind ResolveAttachmentKind()
    {
        if (IsIncome) return AttachmentKind.Other;
        return SelectedAttachmentKind;
    }

    [RelayCommand]
    private async Task RemoveAttachmentAsync()
    {
        if (SelectedAttachment is null) return;
        await _attachmentService.DeleteAsync(SelectedAttachment.Id);
        await ReloadAttachmentsAsync();
    }

    [RelayCommand]
    private void ViewAttachment()
    {
        if (Attachments.Count == 0) return;
        var index = SelectedAttachment is null
            ? 0
            : Math.Max(0, Attachments.ToList().FindIndex(a => a.Id == SelectedAttachment.Id));

        var viewer = _serviceProvider.GetRequiredService<ReceiptViewerViewModel>();
        viewer.Load(Attachments.ToList(), index);
        var window = new ReceiptViewerWindow(viewer)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task ScanAttachmentAsync()
    {
        await BrowseAttachmentAsync();
    }

    private async Task<bool> EnsureTransactionSavedAsync()
    {
        if (_editId != ObjectId.Empty)
            return true;

        if (!AppDialog.Confirm(
                "Save required",
                "Save this transaction before adding receipts?",
                confirmText: "Save",
                cancelText: "Cancel"))
            return false;


        return await PersistAsync(closeAfterSave: false);
    }
}
