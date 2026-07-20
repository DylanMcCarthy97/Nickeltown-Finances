using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Services;

namespace NickeltownFinance.ViewModels;

public enum ImportStartupMode
{
    None,
    Anz,
    Square
}

public partial class ImportViewModel : ViewModelBase
{
    public static ImportStartupMode PendingStartupMode { get; set; } = ImportStartupMode.None;

    private readonly IStatementImportService _importService;
    private readonly ILegacyTreasurerImportService _legacyImportService;
    private readonly ISquareImportService _squareImportService;
    private readonly ICategoryService _categoryService;
    private readonly ICategorisationService _categorisationService;
    private readonly IReconciliationService _reconciliationService;
    private readonly IReceiptImportService _receiptImportService;
    private readonly INotificationService _notificationService;
    private readonly INavigationService _navigationService;

    private static readonly HashSet<string> ReceiptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    private string _sourceFilePath = string.Empty;
    private StatementFormat _sourceFormat = StatementFormat.Csv;
    private string _bankName = "ANZ";
    private ObservableCollection<ImportRowViewModel> _allRows = [];
    private IReadOnlyList<LegacyTreasurerMonthSummary> _legacyMonthSummaries = [];

    [ObservableProperty] private string _section = "Hub";
    [ObservableProperty] private int _wizardStep = 1;
    [ObservableProperty] private string _importType = string.Empty;

    [ObservableProperty] private ObservableCollection<ImportRowViewModel> _rows = [];
    [ObservableProperty] private ObservableCollection<SquareDepositRowViewModel> _squareRows = [];
    [ObservableProperty] private ObservableCollection<ImportHistoryItem> _history = [];
    [ObservableProperty] private ImportHistoryItem? _selectedHistoryItem;
    [ObservableProperty] private ObservableCollection<CategorisationRuleItem> _rules = [];
    [ObservableProperty] private ObservableCollection<Category> _allCategories = [];
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _showResults;
    [ObservableProperty] private bool _showMapping;
    [ObservableProperty] private ImportResult? _lastResult;
    [ObservableProperty] private AnzParseFailure? _parseFailure;
    [ObservableProperty] private string _summaryText = "Import your ANZ bank statement. Review categories, then import.";
    [ObservableProperty] private CategorisationRuleItem? _selectedRule;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _typeFilter = "All";
    [ObservableProperty] private int _mapDateColumn;
    [ObservableProperty] private int _mapAmountColumn = 1;
    [ObservableProperty] private int _mapDescriptionColumn = 2;
    [ObservableProperty] private bool _mapSkipFirstRow;
    [ObservableProperty] private int _transactionsFound;
    [ObservableProperty] private int _transactionsToImport;
    [ObservableProperty] private int _duplicateCount;
    [ObservableProperty] private int _ignoredCount;
    [ObservableProperty] private decimal _estimatedBalanceChange;

    // Hub status cards
    [ObservableProperty] private int _recentImportCount;
    [ObservableProperty] private int _transactionsWaitingReview;
    [ObservableProperty] private int _squareDepositsWaitingMatch;
    [ObservableProperty] private int _duplicatesFound;
    [ObservableProperty] private string _lastBankImportText = "Never";
    [ObservableProperty] private string _lastSquareImportText = "Never";
    [ObservableProperty] private int _matchedDeposits;
    [ObservableProperty] private int _needsReviewCount;
    [ObservableProperty] private decimal _reconciledTotal;

    public bool HasSquareNeedsReview => NeedsReviewCount > 0;

    // Rule editor
    [ObservableProperty] private string _newRuleMatchText = string.Empty;
    [ObservableProperty] private Category? _newRuleCategory;

    public bool IsHub => Section == "Hub";
    public bool IsWizard => Section == "Wizard";
    public bool IsHistory => Section == "History";
    public bool IsRules => Section == "Rules";
    public bool IsAnzImport => ImportType == "ANZ";
    public bool IsSquareImport => ImportType == "Square";
    public bool IsLegacyImport => ImportType == "Legacy";
    public bool ShowsTransactionGrid => IsAnzImport || IsLegacyImport;
    public bool ShowDropZone => !HasPreview && !ShowMapping;
    public bool CanGoNext => WizardStep switch
    {
        1 => !string.IsNullOrWhiteSpace(ImportType),
        2 => HasPreview || ShowMapping,
        3 => HasPreview,
        4 => HasPreview && TransactionsToImport > 0,
        _ => false
    };
    public bool CanGoBack => WizardStep > 1 && WizardStep < 5;
    public bool ShowStep1 => WizardStep == 1;
    public bool ShowStep2 => WizardStep == 2;
    public bool ShowStep3 => WizardStep == 3;
    public bool ShowStep4 => WizardStep == 4;
    public bool ShowStep5 => WizardStep == 5;
    public string WizardTitle => WizardStep switch
    {
        1 => "Choose import type",
        2 => "Select file",
        3 => IsSquareImport ? "Preview deposits" : "Preview transactions",
        4 => IsSquareImport ? "Review deposits" : "Review suggested categories",
        5 => "Import complete",
        _ => "Import"
    };
    public string StepIndicator => IsAnzImport || IsLegacyImport
        ? $"Step {Math.Clamp(WizardStep - 1, 1, 4)} of 4"
        : IsSquareImport
            ? $"Step {Math.Min(WizardStep, 4)} of 4"
            : $"Step {Math.Min(WizardStep, 5)} of 5";

    public IReadOnlyList<string> StatusFilterOptions { get; } = ["All", "Ready", "Needs Review", "Duplicate", "Matched", "Ignored"];
    public IReadOnlyList<string> TypeFilterOptions { get; } = ["All", "Income", "Expense"];
    public IReadOnlyList<int> ColumnIndexOptions { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    public ImportViewModel(
        IStatementImportService importService,
        ILegacyTreasurerImportService legacyImportService,
        ISquareImportService squareImportService,
        ICategoryService categoryService,
        ICategorisationService categorisationService,
        IReconciliationService reconciliationService,
        IReceiptImportService receiptImportService,
        INotificationService notificationService,
        INavigationService navigationService)
    {
        _importService = importService;
        _legacyImportService = legacyImportService;
        _squareImportService = squareImportService;
        _categoryService = categoryService;
        _categorisationService = categorisationService;
        _reconciliationService = reconciliationService;
        _receiptImportService = receiptImportService;
        _notificationService = notificationService;
        _navigationService = navigationService;

        switch (PendingStartupMode)
        {
            case ImportStartupMode.Anz:
                StartAnzImport();
                PendingStartupMode = ImportStartupMode.None;
                break;
            case ImportStartupMode.Square:
                StartSquareImport();
                PendingStartupMode = ImportStartupMode.None;
                break;
        }

        _ = InitializeAsync();
    }

    public async Task<bool> ImportReceiptFilesAsync(IEnumerable<string> files)
    {
        var receiptPaths = files
            .Where(f => ReceiptExtensions.Contains(Path.GetExtension(f)))
            .ToList();
        if (receiptPaths.Count == 0)
            return false;

        BeginBusy("Green flag — queuing receipts…");
        try
        {
            var imported = await _receiptImportService.ImportFromDesktopAsync(receiptPaths);
            _notificationService.ShowSuccess($"{imported.Count} receipt(s) queued for processing.");
            _navigationService.Navigate<ReceiptInboxViewModel>();
            return true;
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
            return false;
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task InitializeAsync()
    {
        _categorisationService.EnsureDefaultRules();
        AllCategories = new ObservableCollection<Category>(await _categoryService.GetAllActiveAsync());
        NewRuleCategory = AllCategories.FirstOrDefault(c => c.Type == CategoryType.Expense);
        await RefreshHubAsync();
        await RefreshHistoryAsync();
        await RefreshRulesAsync();
    }

    partial void OnSectionChanged(string value) => RaiseSectionProps();
    partial void OnWizardStepChanged(int value) => RaiseWizardProps();
    partial void OnImportTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsAnzImport));
        OnPropertyChanged(nameof(IsSquareImport));
        OnPropertyChanged(nameof(IsLegacyImport));
        OnPropertyChanged(nameof(ShowsTransactionGrid));
        OnPropertyChanged(nameof(WizardTitle));
        OnPropertyChanged(nameof(StepIndicator));
        OnPropertyChanged(nameof(CanGoNext));
    }
    partial void OnHasPreviewChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDropZone));
        OnPropertyChanged(nameof(CanGoNext));
    }
    partial void OnShowMappingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDropZone));
        OnPropertyChanged(nameof(CanGoNext));
    }

    private void RaiseSectionProps()
    {
        OnPropertyChanged(nameof(IsHub));
        OnPropertyChanged(nameof(IsWizard));
        OnPropertyChanged(nameof(IsHistory));
        OnPropertyChanged(nameof(IsRules));
    }

    private void RaiseWizardProps()
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(ShowStep1));
        OnPropertyChanged(nameof(ShowStep2));
        OnPropertyChanged(nameof(ShowStep3));
        OnPropertyChanged(nameof(ShowStep4));
        OnPropertyChanged(nameof(ShowStep5));
        OnPropertyChanged(nameof(WizardTitle));
        OnPropertyChanged(nameof(StepIndicator));
    }

    [RelayCommand]
    private async Task ShowHubAsync()
    {
        Section = "Hub";
        await RefreshHubAsync();
    }

    [RelayCommand]
    private void ShowHistory() => Section = "History";

    [RelayCommand]
    private void ShowRules() => Section = "Rules";

    [RelayCommand]
    private void ShowReceiptInbox() => _navigationService.Navigate<ReceiptInboxViewModel>();

    [RelayCommand]
    private void ShowReconciliation() => _navigationService.Navigate<ReconciliationViewModel>();

    [RelayCommand]
    private void StartAnzImport()
    {
        ResetWizardState();
        ImportType = "ANZ";
        WizardStep = 2;
        Section = "Wizard";
        SummaryText = "Select your ANZ bank statement (CSV or Excel).";
    }

    [RelayCommand]
    private void StartLegacyImport()
    {
        ResetWizardState();
        ImportType = "Legacy";
        WizardStep = 2;
        Section = "Wizard";
        SummaryText = "Select your legacy Income and Expense Statement workbook (.xlsx).";
    }

    // Square CSV import hidden for now — use ANZ import and mark transfers by category.
    [RelayCommand]
    private void StartSquareImport()
    {
        _notificationService.ShowInfo(
            "Square CSV import is turned off. Import the ANZ statement, set each Square transfer's category, and open the transaction to also mark other categories when needed.");
        Section = "Hub";
        WizardStep = 1;
    }

    [RelayCommand]
    private void StartWizard()
    {
        ResetWizardState();
        WizardStep = 1;
        Section = "Wizard";
        SummaryText = "Choose what you want to import.";
    }

    [RelayCommand]
    private void SelectAnzType() => ImportType = "ANZ";

    [RelayCommand]
    private void SelectSquareType() => ImportType = "ANZ";

    [RelayCommand]
    private async Task NextStepAsync()
    {
        if (WizardStep == 1)
        {
            if (string.IsNullOrWhiteSpace(ImportType))
            {
                _notificationService.ShowInfo("Choose ANZ Bank Statement or Square Transactions.");
                return;
            }

            WizardStep = 2;
            SummaryText = IsSquareImport
                ? "Select your Square transactions CSV export."
                : "Select your ANZ bank statement (CSV or Excel).";
            return;
        }

        if (WizardStep == 2)
        {
            if (!HasPreview && !ShowMapping)
            {
                await SelectFileAsync();
                if (!HasPreview && !ShowMapping)
                    return;
            }

            WizardStep = 3;
            return;
        }

        if (WizardStep == 3)
        {
            WizardStep = 4;
            return;
        }

        if (WizardStep == 4)
            await ImportSelectedAsync();
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (WizardStep <= 1)
            return;

        if (WizardStep == 5)
        {
            ResetWizardState();
            _ = ShowHubAsync();
            return;
        }

        // ANZ/Legacy/Square flows start at step 2 (file select); back returns to the hub.
        if (WizardStep == 2 && (IsAnzImport || IsLegacyImport || IsSquareImport))
        {
            _ = ShowHubAsync();
            return;
        }

        WizardStep--;
    }

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        if (IsSquareImport)
        {
            var squareDialog = new OpenFileDialog
            {
                Title = "Import Square Transactions",
                Filter = "Square CSV (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (squareDialog.ShowDialog() != true)
                return;
            await AnalyseSquareFileAsync(squareDialog.FileName);
            return;
        }

        if (IsLegacyImport)
        {
            var legacyDialog = new OpenFileDialog
            {
                Title = "Import Legacy Treasurer Report",
                Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (legacyDialog.ShowDialog() != true)
                return;
            await AnalyseLegacyFileAsync(legacyDialog.FileName);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import ANZ Bank Statement",
            Filter = "ANZ statements (*.csv;*.xlsx)|*.csv;*.xlsx|CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true)
            return;
        await AnalyseFileAsync(dialog.FileName);
    }

    public Task AnalyseFileAsync(string filePath) => AnalyseFileAsync(filePath, null);

    public async Task AnalyseFileAsync(string filePath, AnzColumnMapping? mapping)
    {
        BeginBusy("Leaving the pits — reading statement…");
        ErrorMessage = null;
        ShowResults = false;
        ShowMapping = false;
        LastResult = null;
        ParseFailure = null;
        SquareRows = [];
        try
        {
            var result = await _importService.AnalyseAsync(filePath, mapping);
            _sourceFilePath = filePath;

            if (result.Failure is not null)
            {
                ParseFailure = result.Failure;
                ShowMapping = true;
                HasPreview = false;
                _allRows = [];
                Rows = [];
                FileName = result.Failure.FileName;
                SummaryText = "Could not read this file as an ANZ export. Map columns if needed.";
                ErrorMessage = result.Failure.Reason;
                MapDateColumn = 0;
                MapAmountColumn = 1;
                MapDescriptionColumn = Math.Min(2, Math.Max(0, result.Failure.DetectedColumnCount - 1));
                return;
            }

            var preview = result.Preview!;
            _sourceFormat = preview.Format;
            _bankName = preview.BankName;
            FileName = preview.FileName;

            var rows = new ObservableCollection<ImportRowViewModel>();
            foreach (var row in preview.Rows)
            {
                var isIncome = row.Credit > 0;
                var categories = new ObservableCollection<Category>(
                    AllCategories.Where(c => c.Type == (isIncome ? CategoryType.Income : CategoryType.Expense)));

                var suggested = categories.FirstOrDefault(c => c.Id == row.SuggestedCategoryId);
                var vm = new ImportRowViewModel
                {
                    IsSelected = row.IsSelected,
                    Date = row.Date,
                    Description = row.Description,
                    Notes = row.Notes,
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Balance = row.Balance,
                    Reference = row.Reference,
                    Status = row.Status,
                    MatchedTransactionId = row.MatchedTransactionId,
                    Fingerprint = row.Fingerprint,
                    DuplicateConfidence = row.DuplicateConfidence,
                    Error = row.Error,
                    IsSquareDeposit = row.IsSquareDeposit,
                    RememberCategory = row.Status == ImportRowStatus.NeedsReview,
                    Categories = categories,
                    SelectedCategory = suggested
                };
                vm.PropertyChanged += OnRowPropertyChanged;
                rows.Add(vm);
            }

            _allRows = rows;
            ApplyFilters();
            HasPreview = true;
            ShowMapping = false;
            RecalculateSummary();
            SummaryText = $"ANZ statement loaded: {preview.Rows.Count} transactions. Review categories, then import.";

            if (preview.Warnings.Count > 0)
                _notificationService.ShowInfo(string.Join(" ", preview.Warnings.Take(3)));

            if (Section == "Wizard" && WizardStep == 2)
                WizardStep = 3;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasPreview = false;
            Rows = [];
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task AnalyseLegacyFileAsync(string filePath)
    {
        BeginBusy("Leaving the pits — reading legacy workbook…");
        ErrorMessage = null;
        ShowResults = false;
        ShowMapping = false;
        LastResult = null;
        ParseFailure = null;
        SquareRows = [];
        try
        {
            var result = await _legacyImportService.AnalyseAsync(filePath);
            _sourceFilePath = filePath;

            if (result.FailureReason is not null)
            {
                ErrorMessage = result.FailureReason;
                HasPreview = false;
                _allRows = [];
                Rows = [];
                _legacyMonthSummaries = [];
                _notificationService.ShowError(result.FailureReason);
                return;
            }

            var preview = result.Preview!;
            _sourceFormat = StatementFormat.Excel;
            _bankName = "Legacy Treasurer Report";
            FileName = preview.FileName;
            _legacyMonthSummaries = preview.Months;

            var rows = new ObservableCollection<ImportRowViewModel>();
            foreach (var row in preview.Rows)
            {
                var isIncome = row.Credit > 0;
                var categories = new ObservableCollection<Category>(
                    AllCategories.Where(c => c.Type == (isIncome ? CategoryType.Income : CategoryType.Expense)));

                var suggested = categories.FirstOrDefault(c => c.Id == row.SuggestedCategoryId);
                var vm = new ImportRowViewModel
                {
                    IsSelected = row.IsSelected,
                    Date = row.Date,
                    Description = row.Description,
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Fingerprint = row.Fingerprint,
                    Status = row.Status,
                    MatchedTransactionId = row.MatchedTransactionId,
                    Categories = categories,
                    SelectedCategory = suggested
                };
                vm.PropertyChanged += OnRowPropertyChanged;
                rows.Add(vm);
            }

            _allRows = rows;
            ApplyFilters();
            HasPreview = true;
            var monthCount = preview.Months.Count(m => !m.IsSkipped);
            RecalculateSummary();
            var minDate = preview.Rows.Min(r => r.Date);
            var maxDate = preview.Rows.Max(r => r.Date);
            SummaryText =
                $"Legacy workbook loaded: {monthCount} month(s), {preview.Rows.Count} transactions " +
                $"({minDate:MMM yyyy} – {maxDate:MMM yyyy}). " +
                "Missing financial years are created automatically from transaction dates. " +
                "Cash on hand and Shire bonds will be saved per month.";

            if (preview.Warnings.Count > 0)
                _notificationService.ShowInfo(string.Join(" ", preview.Warnings.Take(3)));

            if (Section == "Wizard" && WizardStep == 2)
                WizardStep = 3;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasPreview = false;
            Rows = [];
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task AnalyseSquareFileAsync(string filePath)
    {
        BeginBusy("Leaving the pits — reading Square file…");
        ErrorMessage = null;
        ShowResults = false;
        LastResult = null;
        _allRows = [];
        Rows = [];
        try
        {
            var result = await _squareImportService.AnalyseAsync(filePath);
            _sourceFilePath = filePath;

            if (result.FailureReason is not null)
            {
                ErrorMessage = result.FailureReason;
                HasPreview = false;
                SquareRows = [];
                _notificationService.ShowError(result.FailureReason);
                return;
            }

            var preview = result.Preview!;
            FileName = preview.FileName;
            SquareRows = new ObservableCollection<SquareDepositRowViewModel>(
                preview.Deposits.Select(d => new SquareDepositRowViewModel
                {
                    IsSelected = d.IsSelected,
                    DepositId = d.DepositId,
                    DepositDate = d.DepositDate,
                    GrossAmount = d.GrossAmount,
                    Fees = d.Fees,
                    NetAmount = d.NetAmount,
                    TransactionCount = d.TransactionCount,
                    Status = d.Status,
                    Fingerprint = d.Fingerprint,
                    Lines = d.Lines
                }));

            foreach (var row in SquareRows)
                row.PropertyChanged += OnSquareRowPropertyChanged;

            HasPreview = true;
            RecalculateSummary();
            SummaryText = $"Square file loaded: {SquareRows.Count} deposits. Review, then import.";

            if (preview.Warnings.Count > 0)
                _notificationService.ShowInfo(string.Join(" ", preview.Warnings.Take(3)));

            if (Section == "Wizard" && WizardStep == 2 && IsSquareImport)
                WizardStep = 3;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasPreview = false;
            SquareRows = [];
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private async Task ApplyManualMappingAsync()
    {
        if (string.IsNullOrWhiteSpace(_sourceFilePath))
            return;

        var mapping = new AnzColumnMapping
        {
            DateIndex = MapDateColumn,
            AmountIndex = MapAmountColumn,
            DescriptionIndex = MapDescriptionColumn,
            SkipFirstRow = MapSkipFirstRow
        };

        await AnalyseFileAsync(_sourceFilePath, mapping);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnStatusFilterChanged(string value) => ApplyFilters();
    partial void OnTypeFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allRows.Where(r => r.PassesFilter(SearchText, StatusFilter, TypeFilter));
        Rows = new ObservableCollection<ImportRowViewModel>(filtered);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportRowViewModel.IsSelected) or nameof(ImportRowViewModel.Status)
            or nameof(ImportRowViewModel.Credit) or nameof(ImportRowViewModel.Debit))
        {
            RecalculateSummary();
            OnPropertyChanged(nameof(CanGoNext));
        }
    }

    private void OnSquareRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SquareDepositRowViewModel.IsSelected) or nameof(SquareDepositRowViewModel.Status))
        {
            RecalculateSummary();
            OnPropertyChanged(nameof(CanGoNext));
        }
    }

    private void RecalculateSummary()
    {
        if (IsSquareImport)
        {
            TransactionsFound = SquareRows.Count;
            TransactionsToImport = SquareRows.Count(r => r.IsSelected);
            DuplicateCount = SquareRows.Count(r => r.Status == ImportRowStatus.Duplicate);
            IgnoredCount = SquareRows.Count(r => !r.IsSelected && r.Status != ImportRowStatus.Duplicate);
            EstimatedBalanceChange = SquareRows.Where(r => r.IsSelected).Sum(r => r.NetAmount);
            return;
        }

        TransactionsFound = _allRows.Count;
        TransactionsToImport = _allRows.Count(r => r.IsSelected);
        DuplicateCount = _allRows.Count(r => r.Status == ImportRowStatus.Duplicate);
        IgnoredCount = _allRows.Count(r => r.Status == ImportRowStatus.Ignored || (!r.IsSelected && r.Status != ImportRowStatus.Duplicate));
        EstimatedBalanceChange = _allRows.Where(r => r.IsSelected).Sum(r => r.Credit - r.Debit);
    }

    [RelayCommand]
    private void SelectAllNew()
    {
        if (IsSquareImport)
        {
            foreach (var row in SquareRows)
                row.IsSelected = row.Status is ImportRowStatus.New or ImportRowStatus.NeedsReview;
        }
        else
        {
            foreach (var row in _allRows)
                row.IsSelected = row.Status is ImportRowStatus.New or ImportRowStatus.NeedsReview;
        }

        RecalculateSummary();
    }

    [RelayCommand]
    private void SkipDuplicates()
    {
        if (IsSquareImport)
        {
            foreach (var row in SquareRows.Where(r => r.Status == ImportRowStatus.Duplicate))
                row.IsSelected = false;
        }
        else
        {
            foreach (var row in _allRows.Where(r => r.Status == ImportRowStatus.Duplicate))
                row.IsSelected = false;
        }

        RecalculateSummary();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (IsSquareImport)
        {
            foreach (var row in SquareRows)
                row.IsSelected = true;
        }
        else
        {
            foreach (var row in _allRows)
                row.IsSelected = true;
        }

        RecalculateSummary();
    }

    [RelayCommand]
    private void SelectNone()
    {
        if (IsSquareImport)
        {
            foreach (var row in SquareRows)
                row.IsSelected = false;
        }
        else
        {
            foreach (var row in _allRows)
                row.IsSelected = false;
        }

        RecalculateSummary();
    }

    [RelayCommand]
    private void MarkIgnored()
    {
        foreach (var row in _allRows.Where(r => r.IsSelected))
        {
            row.Status = ImportRowStatus.Ignored;
            row.IsSelected = false;
        }

        RecalculateSummary();
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ImportSelectedAsync()
    {
        RecalculateSummary();
        if (TransactionsToImport == 0)
        {
            _notificationService.ShowInfo("Nothing selected for import.");
            return;
        }

        var committingSquare = IsSquareImport;

        if (!committingSquare)
        {
            var needsCategory = _allRows.Count(r =>
                r.IsSelected && r.SelectedCategory is null);
            if (needsCategory > 0)
            {
                _notificationService.ShowInfo(
                    $"{needsCategory} transaction(s) still need a category. Review categories before importing.");
                WizardStep = 4;
                return;
            }
        }

        var label = committingSquare ? "deposits" : "transactions";
        var confirmMessage =
            $"Found: {TransactionsFound}\n" +
            $"To import: {TransactionsToImport}\n" +
            $"Duplicates: {DuplicateCount}\n" +
            $"Ignored: {IgnoredCount}\n" +
            $"Estimated amount: {EstimatedBalanceChange:C}\n\n" +
            $"Import selected {label}?";

        if (!AppDialog.Confirm("Import summary", confirmMessage, confirmText: "Import", cancelText: "Cancel"))
            return;

        BeginBusy("Green flag — importing…");
        try
        {
            if (committingSquare)
            {
                LastResult = await _squareImportService.CommitAsync(new SquareImportCommitRequest
                {
                    FileName = FileName,
                    Deposits = SquareRows.Select(r => new SquarePreviewRow
                    {
                        IsSelected = r.IsSelected,
                        DepositId = r.DepositId,
                        DepositDate = r.DepositDate,
                        GrossAmount = r.GrossAmount,
                        Fees = r.Fees,
                        NetAmount = r.NetAmount,
                        TransactionCount = r.TransactionCount,
                        Status = r.Status,
                        Fingerprint = r.Fingerprint,
                        Lines = r.Lines
                    }).ToList()
                });
            }
            else if (IsLegacyImport)
            {
                LastResult = await _legacyImportService.CommitAsync(new LegacyTreasurerCommitRequest
                {
                    FileName = FileName,
                    Months = _legacyMonthSummaries,
                    Rows = _allRows.Select(r => new ImportPreviewRow
                    {
                        IsSelected = r.IsSelected,
                        Date = r.Date,
                        Description = r.Description,
                        Debit = r.Debit,
                        Credit = r.Credit,
                        SuggestedCategoryId = r.SelectedCategory?.Id,
                        SuggestedCategoryName = r.SelectedCategory?.Name ?? string.Empty,
                        RememberCategory = r.RememberCategory,
                        Status = r.Status,
                        MatchedTransactionId = r.MatchedTransactionId,
                        Fingerprint = r.Fingerprint
                    }).ToList()
                });
            }
            else
            {
                LastResult = await _importService.CommitAsync(new ImportCommitRequest
                {
                    FileName = FileName,
                    Format = _sourceFormat,
                    BankName = _bankName,
                    Rows = _allRows.Select(r => new ImportPreviewRow
                    {
                        IsSelected = r.IsSelected,
                        Date = r.Date,
                        Description = r.Description,
                        Notes = r.Notes,
                        Debit = r.Debit,
                        Credit = r.Credit,
                        Balance = r.Balance,
                        Reference = r.Reference,
                        SuggestedCategoryId = r.SelectedCategory?.Id,
                        SuggestedCategoryName = r.SelectedCategory?.Name ?? string.Empty,
                        IsSquareDeposit = r.IsSquareDeposit,
                        RememberCategory = r.RememberCategory,
                        Status = r.Status,
                        MatchedTransactionId = r.MatchedTransactionId,
                        Fingerprint = r.Fingerprint,
                        DuplicateConfidence = r.DuplicateConfidence,
                        Error = r.Error
                    }).ToList()
                });
            }

            ShowResults = true;
            WizardStep = 5;
            var fyNote = LastResult.ErrorMessages.FirstOrDefault(m =>
                m.StartsWith("Created ", StringComparison.OrdinalIgnoreCase) &&
                m.Contains("financial year", StringComparison.OrdinalIgnoreCase));
            SummaryText =
                $"Import complete — Imported {LastResult.Imported}, skipped {LastResult.Skipped}, " +
                $"duplicates {LastResult.Duplicates}, errors {LastResult.Errors}" +
                (LastResult.SquareMatched > 0 || LastResult.SquareNeedsReview > 0
                    ? $", Square matched {LastResult.SquareMatched}, needs review {LastResult.SquareNeedsReview}"
                    : string.Empty) +
                (fyNote is not null ? $". {fyNote.TrimEnd('.')}." : ".");

            await RefreshHistoryAsync();
            await RefreshRulesAsync();
            await RefreshHubAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notificationService.ShowError(ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand]
    private void ClearPreview() => ResetWizardState();

    [RelayCommand]
    private async Task FinishWizardAsync()
    {
        ResetWizardState();
        Section = "Hub";
        await RefreshHubAsync();
    }

    private void ResetWizardState()
    {
        HasPreview = false;
        ShowResults = false;
        ShowMapping = false;
        ParseFailure = null;
        LastResult = null;
        _allRows = [];
        Rows = [];
        SquareRows = [];
        _legacyMonthSummaries = [];
        FileName = string.Empty;
        ErrorMessage = null;
        RecalculateSummary();
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        History = new ObservableCollection<ImportHistoryItem>(await _importService.GetHistoryAsync());
    }

    [RelayCommand]
    private async Task RefreshHubAsync()
    {
        var status = await _importService.GetImportStatusAsync();
        RecentImportCount = status.RecentImportCount;
        TransactionsWaitingReview = status.TransactionsWaitingReview;
        SquareDepositsWaitingMatch = status.SquareDepositsWaitingMatch;
        DuplicatesFound = status.DuplicatesFound;
        LastBankImportText = status.LastBankImport is { } bank
            ? bank.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "Never";
        LastSquareImportText = status.LastSquareImport is { } square
            ? square.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "Never";

        var reconciliation = await _reconciliationService.GetSummaryAsync();
        MatchedDeposits = reconciliation.Matched;
        NeedsReviewCount = reconciliation.NeedsReview;
        ReconciledTotal = reconciliation.ReconciledTotal;
        OnPropertyChanged(nameof(HasSquareNeedsReview));
    }

    [RelayCommand]
    private async Task UndoImportAsync()
    {
        if (SelectedHistoryItem is null)
        {
            _notificationService.ShowInfo("Select an import from history to undo.");
            return;
        }

        if (!SelectedHistoryItem.UndoAvailable)
        {
            AppDialog.Info("Undo unavailable", "This import has already been undone or has no data to remove.");
            return;
        }

        if (!AppDialog.Confirm(
                "Undo import",
                $"Remove all data imported from '{SelectedHistoryItem.FileName}' on {SelectedHistoryItem.ImportedAt:g}?",
                confirmText: "Undo import",
                cancelText: "Cancel",
                isDanger: true))
            return;

        var isSquare = SelectedHistoryItem.SourceType.Contains("Square", StringComparison.OrdinalIgnoreCase);
        var isLegacy = SelectedHistoryItem.SourceType.Contains("Legacy", StringComparison.OrdinalIgnoreCase);
        var (success, error) = isSquare
            ? await _squareImportService.UndoImportAsync(SelectedHistoryItem.Id)
            : isLegacy
                ? await _legacyImportService.UndoImportAsync(SelectedHistoryItem.Id)
                : await _importService.UndoImportAsync(SelectedHistoryItem.Id);

        if (!success)
        {
            AppDialog.Error("Unable to undo", error ?? "Unknown error.");
            return;
        }

        _notificationService.ShowSuccess("Import undone.");
        await RefreshHistoryAsync();
        await RefreshHubAsync();
    }

    [RelayCommand]
    private async Task RefreshRulesAsync()
    {
        Rules = new ObservableCollection<CategorisationRuleItem>(await _categorisationService.GetRulesAsync());
    }

    [RelayCommand]
    private async Task AddRuleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRuleMatchText) || NewRuleCategory is null)
        {
            _notificationService.ShowInfo("Enter match text and choose a category.");
            return;
        }

        await _categorisationService.SaveRuleAsync(new CategorisationRule
        {
            MatchText = NewRuleMatchText.Trim().ToUpperInvariant(),
            CategoryId = NewRuleCategory.Id,
            Action = CategorisationRuleAction.AssignCategory,
            Priority = 20
        });

        NewRuleMatchText = string.Empty;
        await RefreshRulesAsync();
        _notificationService.ShowSuccess("Rule saved.");
    }

    [RelayCommand]
    private void OpenMobileReceiptUpload()
    {
        var vm = App.Services.GetRequiredService<ViewModels.Dialogs.MobileReceiptUploadViewModel>();
        var window = new Views.Dialogs.MobileReceiptUploadWindow(vm, new MobileUploadSessionRequest
        {
            ImportTarget = ReceiptImportTarget.Inbox
        })
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private async Task DeleteRuleAsync()
    {
        if (SelectedRule is null) return;
        await _categorisationService.DeleteRuleAsync(SelectedRule.Id);
        await RefreshRulesAsync();
        _notificationService.ShowInfo("Categorisation rule removed.");
    }
}
