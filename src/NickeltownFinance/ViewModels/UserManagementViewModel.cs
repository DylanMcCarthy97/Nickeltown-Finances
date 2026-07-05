using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels.Dialogs;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.ViewModels;

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly INotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<UserRowViewModel> _allUsers = [];

    [ObservableProperty] private ObservableCollection<UserRowViewModel> _users = [];
    [ObservableProperty] private UserRowViewModel? _selectedUser;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _roleFilter;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isDetailsVisible;

    // Editable detail fields
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editDisplayName = string.Empty;
    [ObservableProperty] private UserRole _editRole;
    [ObservableProperty] private bool _editIsActive;
    [ObservableProperty] private string? _editEmail;
    [ObservableProperty] private string? _editProfilePicturePath;
    [ObservableProperty] private string? _editSignatureImagePath;
    [ObservableProperty] private bool _editHasSignature;
    [ObservableProperty] private string _detailCreated = string.Empty;
    [ObservableProperty] private string _detailLastLogin = string.Empty;
    [ObservableProperty] private string _detailPasswordChanged = string.Empty;
    [ObservableProperty] private string _detailStatusText = string.Empty;
    [ObservableProperty] private string _detailStatusKind = "Inactive";
    [ObservableProperty] private string _detailRoleDisplay = string.Empty;
    [ObservableProperty] private string _detailAvatarInitials = "?";
    [ObservableProperty] private bool _detailIsLocked;
    [ObservableProperty] private string _activateDeactivateLabel = "Deactivate Account";

    public string[] RoleFilterOptions { get; } =
    [
        "All Roles",
        "Administrator",
        "Treasurer",
        "Committee",
        "Read Only"
    ];

    public UserRole[] Roles { get; } =
    [
        UserRole.Administrator,
        UserRole.Treasurer,
        UserRole.Committee,
        UserRole.ReadOnly
    ];

    public UserManagementViewModel(
        IUserService userService,
        ISessionService sessionService,
        INotificationService notificationService,
        IServiceProvider serviceProvider)
    {
        _userService = userService;
        _sessionService = sessionService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        RoleFilter = RoleFilterOptions[0];
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnRoleFilterChanged(string? value) => ApplyFilter();

    partial void OnEditRoleChanged(UserRole value) =>
        DetailRoleDisplay = value.ToDisplayName();

    partial void OnSelectedUserChanged(UserRowViewModel? value)
    {
        HasSelection = value is not null;
        IsDetailsVisible = value is not null;
        if (value is null)
            return;

        LoadDetailsFrom(value);
    }

    public async Task LoadAsync()
    {
        var selectedId = SelectedUser?.Source.Id;
        var users = await _userService.GetAllAsync();

        _allUsers.Clear();
        foreach (var user in users)
            _allUsers.Add(new UserRowViewModel(user));

        ApplyFilter();

        if (selectedId is { } id)
            SelectedUser = Users.FirstOrDefault(u => u.Source.Id == id)
                           ?? Users.FirstOrDefault();
        else
            SelectedUser = Users.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        IEnumerable<UserRowViewModel> query = _allUsers;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(u =>
                u.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.RoleDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(RoleFilter) && RoleFilter != "All Roles")
            query = query.Where(u => u.RoleDisplay == RoleFilter);

        var selectedId = SelectedUser?.Source.Id;
        Users = new ObservableCollection<UserRowViewModel>(query);

        if (selectedId is { } id)
            SelectedUser = Users.FirstOrDefault(u => u.Source.Id == id);
    }

    private void LoadDetailsFrom(UserRowViewModel row)
    {
        EditUsername = row.Username;
        EditDisplayName = row.DisplayName;
        EditRole = row.Role;
        EditIsActive = row.IsActive;
        EditEmail = row.Email;
        EditProfilePicturePath = row.ProfilePicturePath;
        EditSignatureImagePath = row.SignatureImagePath;
        EditHasSignature = row.HasSignature;
        DetailCreated = row.CreatedDisplay;
        DetailLastLogin = row.LastLoginDisplay;
        DetailPasswordChanged = row.PasswordChangedDisplay;
        DetailStatusText = row.StatusText;
        DetailStatusKind = row.StatusKind;
        DetailRoleDisplay = row.RoleDisplay;
        DetailAvatarInitials = row.AvatarInitials;
        DetailIsLocked = row.IsLocked;
        ActivateDeactivateLabel = row.IsActive ? "Deactivate Account" : "Activate Account";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task NewUserAsync()
    {
        var vm = _serviceProvider.GetRequiredService<NewUserDialogViewModel>();
        var window = new NewUserDialogWindow(vm)
        {
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await LoadAsync();
            _notificationService.ShowSuccess("User created.");
        }
    }

    [RelayCommand]
    private void OpenUser()
    {
        if (SelectedUser is null)
        {
            _notificationService.ShowError("Select a user to open.");
            return;
        }

        IsDetailsVisible = true;
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (SelectedUser is null) return;

        var profilePath = EditProfilePicturePath;
        if (!string.IsNullOrWhiteSpace(profilePath) &&
            !profilePath.StartsWith(AppPaths.FilesRoot, StringComparison.OrdinalIgnoreCase))
        {
            profilePath = UserFileStorage.StoreProfilePicture(SelectedUser.Source.Id, profilePath);
            EditProfilePicturePath = profilePath;
        }

        var signaturePath = EditSignatureImagePath;
        if (!string.IsNullOrWhiteSpace(signaturePath) &&
            !signaturePath.StartsWith(AppPaths.FilesRoot, StringComparison.OrdinalIgnoreCase))
        {
            signaturePath = UserFileStorage.StoreSignature(SelectedUser.Source.Id, signaturePath);
            EditSignatureImagePath = signaturePath;
            EditHasSignature = true;
        }

        var (success, error) = await _userService.UpdateAsync(
            SelectedUser.Source.Id,
            EditUsername,
            EditDisplayName,
            EditRole,
            EditIsActive,
            EditEmail,
            profilePath,
            signaturePath);

        if (!success)
        {
            ErrorMessage = error;
            _notificationService.ShowError(error ?? "Unable to save changes.");
            return;
        }

        ErrorMessage = null;
        await LoadAsync();
        _notificationService.ShowSuccess("User updated.");
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser is null) return;

        if (SelectedUser.Source.Id == _sessionService.CurrentUser?.Id)
        {
            _notificationService.ShowError("You cannot delete your own account.");
            return;
        }

        if (SelectedUser.Role == UserRole.Administrator &&
            _allUsers.Count(u => u.Role == UserRole.Administrator && u.Source.Id != SelectedUser.Source.Id) == 0)
        {
            _notificationService.ShowError("Cannot delete the last administrator.");
            return;
        }

        var roleName = SelectedUser.RoleDisplay;
        if (!AppDialog.Confirm(
                $"Delete {roleName}?",
                "This cannot be undone.",
                confirmText: "Delete",
                cancelText: "Cancel",
                isDanger: true))
            return;

        var (success, error) = await _userService.DeleteAsync(SelectedUser.Source.Id);
        if (!success)
        {
            _notificationService.ShowError(error ?? "Unable to delete user.");
            return;
        }

        SelectedUser = null;
        await LoadAsync();
        _notificationService.ShowSuccess("User deleted.");
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null) return;

        var vm = _serviceProvider.GetRequiredService<ResetPasswordDialogViewModel>();
        vm.Initialize(SelectedUser.Source.Id, SelectedUser.Username, SelectedUser.DisplayName);
        var window = new ResetPasswordDialogWindow(vm)
        {
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await LoadAsync();
            _notificationService.ShowSuccess("Password reset successfully.");
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null) return;

        var activate = !SelectedUser.IsActive;
        var action = activate ? "activate" : "deactivate";

        if (!activate)
        {
            if (SelectedUser.Source.Id == _sessionService.CurrentUser?.Id)
            {
                _notificationService.ShowError("You cannot deactivate your own account.");
                return;
            }

            if (SelectedUser.Role == UserRole.Administrator &&
                _allUsers.Count(u => u.Role == UserRole.Administrator && u.IsActive && u.Source.Id != SelectedUser.Source.Id) == 0)
            {
                _notificationService.ShowError("Cannot deactivate the last administrator.");
                return;
            }

            if (!AppDialog.Confirm(
                    "Deactivate Account",
                    $"Deactivate '{SelectedUser.DisplayName}'? They will not be able to sign in.\n\nYou can reactivate this account later.",
                    confirmText: "Deactivate",
                    cancelText: "Cancel",
                    isDanger: true))
                return;
        }

        var (success, error) = await _userService.SetActiveAsync(SelectedUser.Source.Id, activate);
        if (!success)
        {
            _notificationService.ShowError(error ?? $"Unable to {action} user.");
            return;
        }

        await LoadAsync();
        _notificationService.ShowSuccess(activate ? "User activated." : "User deactivated.");
    }

    [RelayCommand]
    private async Task UnlockUserAsync()
    {
        if (SelectedUser is null || !SelectedUser.IsLocked) return;

        var (success, error) = await _userService.UnlockAsync(SelectedUser.Source.Id);
        if (!success)
        {
            _notificationService.ShowError(error ?? "Unable to unlock user.");
            return;
        }

        await LoadAsync();
        _notificationService.ShowSuccess("User unlocked.");
    }

    [RelayCommand]
    private void BrowseProfilePicture()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
        };
        if (dialog.ShowDialog() == true)
            EditProfilePicturePath = dialog.FileName;
    }

    [RelayCommand]
    private void ClearProfilePicture() => EditProfilePicturePath = null;

    [RelayCommand]
    private void DrawSignature()
    {
        var captured = SignatureCaptureWindow.Capture(Application.Current.MainWindow);
        if (captured is null)
            return;

        EditSignatureImagePath = captured;
        EditHasSignature = true;
    }

    [RelayCommand]
    private void BrowseSignature()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select digital signature",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
        };
        if (dialog.ShowDialog() != true)
            return;

        EditSignatureImagePath = dialog.FileName;
        EditHasSignature = true;
    }

    [RelayCommand]
    private void ClearSignature()
    {
        EditSignatureImagePath = null;
        EditHasSignature = false;
    }
}
