using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Microsoft.Win32;
using NickeltownFinance.Core;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Core.Update;
using NickeltownFinance.Services;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ICategoryService _categoryService;
    private readonly IBackupService _backupService;
    private readonly INotificationService _notificationService;
    private readonly ISessionService _sessionService;
    private readonly IUserService _userService;
    private readonly IReceiptProcessingSettingsService _receiptProcessingSettings;
    private readonly IAppUpdateService _appUpdateService;

    private AppUpdateInfo? _pendingUpdate;

    private List<Category> _allIncomeCategories = [];
    private List<Category> _allExpenseCategories = [];

    [ObservableProperty] private string _clubName = string.Empty;
    [ObservableProperty] private string? _clubLogoPath;
    [ObservableProperty] private int _financialYearStartMonth = 7;
    [ObservableProperty] private AppTheme _selectedTheme = AppTheme.System;
    [ObservableProperty] private string _backupFolder = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Category> _incomeCategories = [];
    [ObservableProperty] private ObservableCollection<Category> _expenseCategories = [];
    [ObservableProperty] private ObservableCollection<BackupInfo> _backupHistory = [];
    [ObservableProperty] private BackupInfo? _selectedBackup;
    [ObservableProperty] private string _newCategoryName = string.Empty;
    [ObservableProperty] private CategoryType _newCategoryType = CategoryType.Income;
    [ObservableProperty] private Category? _selectedCategoryForEdit;
    [ObservableProperty] private string _renameCategoryName = string.Empty;
    [ObservableProperty] private Category? _mergeSourceCategory;
    [ObservableProperty] private Category? _mergeTargetCategory;
    [ObservableProperty] private ObservableCollection<Category> _allCategories = [];
    [ObservableProperty] private string _selectedSection = "Club";
    [ObservableProperty] private bool _isAdministrator;
    [ObservableProperty] private string? _mySignatureImagePath;
    [ObservableProperty] private bool _hasMySignature;
    [ObservableProperty] private bool _receiptAutoEnhancement = true;
    [ObservableProperty] private bool _receiptOcrEnabled = true;
    [ObservableProperty] private bool _receiptAiCategorisation = true;
    [ObservableProperty] private bool _receiptBankMatching = true;
    [ObservableProperty] private bool _receiptDuplicateDetection = true;
    [ObservableProperty] private bool _receiptThumbnailGeneration = true;
    [ObservableProperty] private string _updateStatusText = "Check GitHub releases for new versions.";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string? _availableUpdateVersion;
    [ObservableProperty] private string? _availableUpdateNotes;
    [ObservableProperty] private bool _isCheckingForUpdates;
    [ObservableProperty] private bool _isInstallingUpdate;
    [ObservableProperty] private bool _showUpdateProgress;
    [ObservableProperty] private double _updateProgressPercent;
    [ObservableProperty] private bool _isUpdateProgressIndeterminate;
    [ObservableProperty] private string _updateStageLabel = "On track";
    [ObservableProperty] private string _updateProgressLabel = "0%";
    [ObservableProperty] private string _updateDetailText = string.Empty;

    public string AppVersion => AppInfo.VersionLabel;
    public string InstallKindLabel => _appUpdateService.InstallKind == AppInstallKind.Msix ? "MSIX package" : "Portable";
    public bool UpdateFeedConfigured => UpdateConstants.IsConfigured;

    public UserManagementViewModel Users { get; }

    public int[] Months => Enumerable.Range(1, 12).ToArray();
    public Array Themes => Enum.GetValues(typeof(AppTheme));
    public Array CategoryTypes => Enum.GetValues(typeof(CategoryType));

    public bool ShowClub => SelectedSection == "Club";
    public bool ShowUsers => SelectedSection == "Users";
    public bool ShowCategories => SelectedSection == "Categories";
    public bool ShowBackup => SelectedSection == "Backup";
    public bool ShowAppearance => SelectedSection == "Appearance";
    public bool ShowReports => SelectedSection == "Reports";
    public bool ShowReceiptProcessing => SelectedSection == "ReceiptProcessing";
    public bool ShowAbout => SelectedSection == "About";

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        ICategoryService categoryService,
        IBackupService backupService,
        INotificationService notificationService,
        ISessionService sessionService,
        IUserService userService,
        IReceiptProcessingSettingsService receiptProcessingSettings,
        IAppUpdateService appUpdateService,
        UserManagementViewModel usersViewModel)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _categoryService = categoryService;
        _backupService = backupService;
        _notificationService = notificationService;
        _sessionService = sessionService;
        _userService = userService;
        _receiptProcessingSettings = receiptProcessingSettings;
        _appUpdateService = appUpdateService;
        Users = usersViewModel;
        IsAdministrator = _sessionService.CurrentUser?.Role == UserRole.Administrator;
        _ = LoadAsync();
    }

    public void ShowSection(string section)
    {
        if (section == "Users" && !IsAdministrator)
            section = "Club";
        SelectedSection = section;
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowClub));
        OnPropertyChanged(nameof(ShowUsers));
        OnPropertyChanged(nameof(ShowCategories));
        OnPropertyChanged(nameof(ShowBackup));
        OnPropertyChanged(nameof(ShowAppearance));
        OnPropertyChanged(nameof(ShowReports));
        OnPropertyChanged(nameof(ShowReceiptProcessing));
        OnPropertyChanged(nameof(ShowAbout));
    }

    [RelayCommand]
    private void SelectSection(string section) => ShowSection(section);

    public async Task LoadAsync()

    {

        ClubName = _settingsService.ClubName;

        ClubLogoPath = _settingsService.ClubLogoPath;

        FinancialYearStartMonth = _settingsService.FinancialYearStartMonth;

        var me = _sessionService.CurrentUser;
        MySignatureImagePath = me?.SignatureImagePath;
        HasMySignature = !string.IsNullOrWhiteSpace(MySignatureImagePath) && File.Exists(MySignatureImagePath);

        SelectedTheme = _settingsService.Theme;

        BackupFolder = _settingsService.BackupFolder;

        var receiptSettings = _receiptProcessingSettings.GetSettings();
        ReceiptAutoEnhancement = receiptSettings.AutoImageEnhancement;
        ReceiptOcrEnabled = receiptSettings.OcrEnabled;
        ReceiptAiCategorisation = receiptSettings.AiCategorisation;
        ReceiptBankMatching = receiptSettings.BankMatching;
        ReceiptDuplicateDetection = receiptSettings.DuplicateDetection;
        ReceiptThumbnailGeneration = receiptSettings.ThumbnailGeneration;



        _allIncomeCategories = (await _categoryService.GetByTypeAsync(CategoryType.Income)).ToList();

        _allExpenseCategories = (await _categoryService.GetByTypeAsync(CategoryType.Expense)).ToList();

        AllCategories = new ObservableCollection<Category>(_allIncomeCategories.Concat(_allExpenseCategories));
        ApplyCategorySearch();



        var history = await _backupService.GetBackupHistoryAsync();

        BackupHistory = new ObservableCollection<BackupInfo>(history);

    }



    partial void OnSearchTextChanged(string value) => ApplyCategorySearch();

    partial void OnSelectedCategoryForEditChanged(Category? value) =>
        RenameCategoryName = value?.Name ?? string.Empty;

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        // Apply immediately so Light/Dark is visible without waiting for Save.
        _themeService.ApplyTheme(value);
    }



    private void ApplyCategorySearch()

    {

        var query = SearchText?.Trim() ?? string.Empty;

        IncomeCategories = new ObservableCollection<Category>(

            string.IsNullOrEmpty(query)

                ? _allIncomeCategories

                : _allIncomeCategories.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

        ExpenseCategories = new ObservableCollection<Category>(

            string.IsNullOrEmpty(query)

                ? _allExpenseCategories

                : _allExpenseCategories.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

    }





    [RelayCommand]

    private void BrowseLogo()

    {

        var dialog = new OpenFileDialog

        {

            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"

        };

        if (dialog.ShowDialog() == true)

            ClubLogoPath = dialog.FileName;

    }

    [RelayCommand]
    private async Task DrawMySignatureAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null)
        {
            _notificationService.ShowError("You must be signed in.");
            return;
        }

        var captured = SignatureCaptureWindow.Capture(Application.Current.MainWindow);
        if (captured is null)
            return;

        await SaveMySignatureFromPathAsync(user.Id, captured);
    }

    [RelayCommand]
    private async Task BrowseMySignatureAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null)
        {
            _notificationService.ShowError("You must be signed in.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select your digital signature",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
        };
        if (dialog.ShowDialog() != true)
            return;

        await SaveMySignatureFromPathAsync(user.Id, dialog.FileName);
    }

    private async Task SaveMySignatureFromPathAsync(ObjectId userId, string sourcePath)
    {
        try
        {
            var path = UserFileStorage.StoreSignature(userId, sourcePath);
            var (success, error) = await _userService.UpdateSignatureAsync(userId, path);
            if (!success)
            {
                _notificationService.ShowError(error ?? "Unable to save signature.");
                return;
            }

            MySignatureImagePath = path;
            HasMySignature = true;
            _notificationService.ShowSuccess("Your signature was saved. It will appear on reports you export.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ClearMySignatureAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null) return;

        var (success, error) = await _userService.UpdateSignatureAsync(user.Id, null);
        if (!success)
        {
            _notificationService.ShowError(error ?? "Unable to remove signature.");
            return;
        }

        MySignatureImagePath = null;
        HasMySignature = false;
        _notificationService.ShowSuccess("Your signature was removed.");
    }



    [RelayCommand]

    private void BrowseBackupFolder()

    {

        var dialog = new OpenFolderDialog();

        if (dialog.ShowDialog() == true)

            BackupFolder = dialog.FolderName;

    }



    [RelayCommand]

    private void SaveSettings()

    {

        _settingsService.ClubName = ClubName;

        _settingsService.ClubLogoPath = ClubLogoPath;

        _settingsService.FinancialYearStartMonth = FinancialYearStartMonth;

        _settingsService.Theme = SelectedTheme;

        _settingsService.BackupFolder = BackupFolder;

        _receiptProcessingSettings.SaveSettings(new ReceiptProcessingSettingsInfo
        {
            AutoImageEnhancement = ReceiptAutoEnhancement,
            OcrEnabled = ReceiptOcrEnabled,
            AiCategorisation = ReceiptAiCategorisation,
            BankMatching = ReceiptBankMatching,
            DuplicateDetection = ReceiptDuplicateDetection,
            ThumbnailGeneration = ReceiptThumbnailGeneration
        });

        _settingsService.Save();

        _themeService.ApplyTheme(SelectedTheme);

        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel main)

            main.RefreshBranding();

        _notificationService.ShowSuccess("Settings saved.");

    }



    [RelayCommand]

    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            _notificationService.ShowInfo("Enter a category name.");
            return;
        }

        try
        {
            var cat = new Category
            {
                Name = NewCategoryName.Trim(),
                Type = NewCategoryType,
                Colour = NewCategoryType == CategoryType.Income ? "#4CAF50" : "#E53935",
                Icon = NewCategoryType == CategoryType.Income ? "Money24" : "Cart24",
                SortOrder = 99
            };

            await _categoryService.SaveAsync(cat);
            NewCategoryName = string.Empty;
            await LoadAsync();
            _notificationService.ShowSuccess("Category added.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RenameCategoryAsync()
    {
        if (SelectedCategoryForEdit is null)
        {
            _notificationService.ShowInfo("Select a category to rename.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RenameCategoryName))
        {
            _notificationService.ShowInfo("Enter a new category name.");
            return;
        }

        try
        {
            await _categoryService.RenameAsync(SelectedCategoryForEdit.Id, RenameCategoryName.Trim());
            await LoadAsync();
            _notificationService.ShowSuccess("Category renamed.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MergeCategoriesAsync()
    {
        if (MergeSourceCategory is null || MergeTargetCategory is null)
        {
            _notificationService.ShowInfo("Select two categories to merge.");
            return;
        }

        if (MergeSourceCategory.Id == MergeTargetCategory.Id)
        {
            _notificationService.ShowInfo("Choose two different categories.");
            return;
        }

        if (!AppDialog.Confirm(
                "Merge categories",
                $"Move all transactions from \"{MergeSourceCategory.Name}\" into \"{MergeTargetCategory.Name}\" and archive the source?",
                confirmText: "Merge",
                cancelText: "Cancel"))
            return;

        try
        {
            await _categoryService.MergeAsync(MergeTargetCategory.Id, MergeSourceCategory.Id);
            MergeSourceCategory = null;
            MergeTargetCategory = null;
            SelectedCategoryForEdit = null;
            await LoadAsync();
            _notificationService.ShowSuccess("Categories merged.");
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task ArchiveCategoryAsync(Category? category)
    {
        if (category is null) return;
        await _categoryService.ArchiveAsync(category.Id);
        await LoadAsync();
        _notificationService.ShowSuccess("Category archived.");
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category? category)
    {
        if (category is null) return;

        if (!await _categoryService.CanDeleteAsync(category.Id))
        {
            await _categoryService.ArchiveAsync(category.Id);
            await LoadAsync();
            _notificationService.ShowSuccess("Category is in use — archived instead of deleted.");
            return;
        }

        await _categoryService.DeleteAsync(category.Id);
        await LoadAsync();
        _notificationService.ShowSuccess("Category removed.");
    }



    [RelayCommand]

    private async Task BackupNowAsync()

    {

        var path = await _backupService.CreateBackupAsync(isManual: true);

        await LoadAsync();

        _notificationService.ShowSuccess($"Backup created: {Path.GetFileName(path)}");

    }



    [RelayCommand]

    private async Task RestoreBackupAsync()

    {

        string? path = SelectedBackup?.FilePath;

        if (string.IsNullOrWhiteSpace(path))

        {

            var dialog = new OpenFileDialog

            {

                Filter = "Backup files|*.zip;*.db|Zip backups|*.zip|Legacy database|*.db|All files|*.*",



                InitialDirectory = BackupFolder

            };

            if (dialog.ShowDialog() != true) return;

            path = dialog.FileName;

        }



        try
        {
            _backupService.ValidateBackupFile(path);
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(ex.Message);
            return;
        }

        if (!AppDialog.Confirm(
                "Restore backup",
                "Restoring will replace all current data, receipts, and settings. The application will restart. Continue?",
                confirmText: "Restore",
                cancelText: "Cancel",
                isDanger: true))
            return;

        try
        {
            await _backupService.RestoreBackupAsync(path);
            _notificationService.ShowSuccess("Backup restored. Restarting…");
            App.SkipShutdownBackup = true;
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError($"Restore failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates)
            return;

        IsCheckingForUpdates = true;
        UpdateStatusText = "Checking for updates…";
        ShowUpdateProgress = false;
        UpdateAvailable = false;
        AvailableUpdateVersion = null;
        AvailableUpdateNotes = null;
        _pendingUpdate = null;

        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync();
            if (!result.CheckSucceeded)
            {
                UpdateStatusText = result.ErrorMessage ?? "Update check failed.";
                return;
            }

            if (!result.UpdateAvailable || result.Update is null)
            {
                UpdateStatusText = $"You're on the latest version ({AppVersion}).";
                return;
            }

            _pendingUpdate = result.Update;
            UpdateAvailable = true;
            AvailableUpdateVersion = $"v{result.Update.Version}";
            AvailableUpdateNotes = TruncateReleaseNotes(result.Update.ReleaseNotes);
            UpdateStatusText = $"Version {AvailableUpdateVersion} is available.";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync()
    {
        if (_pendingUpdate is null || IsInstallingUpdate)
            return;

        var message = _appUpdateService.InstallKind == AppInstallKind.Msix
            ? $"Install version {_pendingUpdate.Version}? The app will restart when the update completes."
            : $"Install version {_pendingUpdate.Version} as an MSIX package? Windows App Installer will open and this app will close.";

        if (!AppDialog.Confirm("Install update", message, confirmText: "Install", cancelText: "Not now"))
            return;

        IsInstallingUpdate = true;
        ShowUpdateProgress = true;
        IsUpdateProgressIndeterminate = false;
        UpdateProgressPercent = 0;
        UpdateStageLabel = "Leaving the pits — downloading";
        UpdateProgressLabel = "0%";
        UpdateDetailText = "Fetching the latest MSIX package…";
        UpdateStatusText = "Downloading update…";
        // Install may force-kill the process before Shutdown runs — skip backup either way.
        App.SkipShutdownBackup = true;

        try
        {
            var progress = new Progress<AppUpdateProgress>(ApplyUpdateProgress);
            var result = await _appUpdateService.DownloadAndApplyUpdateAsync(_pendingUpdate, progress);
            if (!result.Success)
            {
                ShowUpdateProgress = false;
                App.SkipShutdownBackup = false;
                UpdateStatusText = result.ErrorMessage ?? "Update failed.";
                _notificationService.ShowError(UpdateStatusText);
                return;
            }

            if (result.RestartRequired)
            {
                UpdateStatusText = "Update installed. Restarting…";
                UpdateProgressPercent = 100;
                IsUpdateProgressIndeterminate = false;
                UpdateStageLabel = "Finish line — restarting";
                UpdateProgressLabel = "100%";
                UpdateDetailText = "Update installed. Restarting the app…";
                _notificationService.ShowSuccess("Update installed. Restarting…");
                Application.Current.Shutdown();
            }
        }
        finally
        {
            IsInstallingUpdate = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenReleaseNotes))]
    private async Task OpenReleaseNotesAsync()
    {
        if (_pendingUpdate is null)
            return;

        await _appUpdateService.OpenReleaseNotesAsync(_pendingUpdate);
    }

    private bool CanInstallUpdate() => UpdateAvailable && _pendingUpdate is not null && !IsInstallingUpdate;

    private bool CanOpenReleaseNotes() => _pendingUpdate is not null;

    private void ApplyUpdateProgress(AppUpdateProgress progress)
    {
        UpdateStatusText = progress.Message;
        UpdateDetailText = progress.Message;
        ShowUpdateProgress = true;
        UpdateStageLabel = progress.Stage switch
        {
            AppUpdateStage.Downloading => "Leaving the pits — downloading",
            AppUpdateStage.Installing => "Pit stop — installing",
            AppUpdateStage.Restarting => "Finish line — restarting",
            _ => "On track"
        };

        if (progress.PercentComplete is double percent)
        {
            IsUpdateProgressIndeterminate = false;
            UpdateProgressPercent = percent;
            UpdateProgressLabel = $"{Math.Clamp((int)Math.Round(percent), 0, 100)}%";
        }
        else
        {
            IsUpdateProgressIndeterminate = true;
            UpdateProgressLabel = "…";
        }
    }

    partial void OnUpdateAvailableChanged(bool value)
    {
        InstallUpdateCommand.NotifyCanExecuteChanged();
        OpenReleaseNotesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInstallingUpdateChanged(bool value) =>
        InstallUpdateCommand.NotifyCanExecuteChanged();

    private static string TruncateReleaseNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "No release notes provided.";

        const int maxLength = 500;
        return notes.Length <= maxLength ? notes : notes[..maxLength].TrimEnd() + "…";
    }
}


