using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ISettingsService _settingsService;
    private readonly IUserRepository _userRepository;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberUsername = true;
    private bool _isPasswordVisible;
    private bool _showForgotPassword;
    private string _adminUsername = string.Empty;
    private string _adminPassword = string.Empty;
    private bool _isAdminPasswordVisible;
    private ResetUserOption? _selectedResetUser;
    private string _resetNewPassword = string.Empty;
    private string _resetConfirmPassword = string.Empty;
    private bool _isResetPasswordVisible;
    private int _resetPasswordStrength;
    private string _resetPasswordStrengthLabel = "Very weak";
    private string? _resetMessage;
    private string? _successMessage;

    public event Action? LoginSucceeded;
    public event Action? ExitRequested;

    public ObservableCollection<ResetUserOption> ResetUsers { get; } = [];

    public string AppVersion => AppInfo.VersionLabel;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool RememberUsername
    {
        get => _rememberUsername;
        set => SetProperty(ref _rememberUsername, value);
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set => SetProperty(ref _isPasswordVisible, value);
    }

    public bool ShowForgotPassword
    {
        get => _showForgotPassword;
        set => SetProperty(ref _showForgotPassword, value);
    }

    public string AdminUsername
    {
        get => _adminUsername;
        set => SetProperty(ref _adminUsername, value);
    }

    public string AdminPassword
    {
        get => _adminPassword;
        set => SetProperty(ref _adminPassword, value);
    }

    public bool IsAdminPasswordVisible
    {
        get => _isAdminPasswordVisible;
        set => SetProperty(ref _isAdminPasswordVisible, value);
    }

    public ResetUserOption? SelectedResetUser
    {
        get => _selectedResetUser;
        set => SetProperty(ref _selectedResetUser, value);
    }

    public string ResetNewPassword
    {
        get => _resetNewPassword;
        set
        {
            if (!SetProperty(ref _resetNewPassword, value))
                return;

            ResetPasswordStrength = PasswordRules.GetStrength(value);
            ResetPasswordStrengthLabel = PasswordRules.GetStrengthLabel(ResetPasswordStrength);
        }
    }

    public string ResetConfirmPassword
    {
        get => _resetConfirmPassword;
        set => SetProperty(ref _resetConfirmPassword, value);
    }

    public bool IsResetPasswordVisible
    {
        get => _isResetPasswordVisible;
        set => SetProperty(ref _isResetPasswordVisible, value);
    }

    public int ResetPasswordStrength
    {
        get => _resetPasswordStrength;
        set => SetProperty(ref _resetPasswordStrength, value);
    }

    public string ResetPasswordStrengthLabel
    {
        get => _resetPasswordStrengthLabel;
        set => SetProperty(ref _resetPasswordStrengthLabel, value);
    }

    public string? ResetMessage
    {
        get => _resetMessage;
        set => SetProperty(ref _resetMessage, value);
    }

    public string? SuccessMessage
    {
        get => _successMessage;
        set => SetProperty(ref _successMessage, value);
    }

    public ICommand TogglePasswordVisibilityCommand { get; }
    public ICommand ToggleAdminPasswordVisibilityCommand { get; }
    public ICommand ToggleResetPasswordVisibilityCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenForgotPasswordCommand { get; }
    public ICommand CloseForgotPasswordCommand { get; }
    public ICommand ResetPasswordCommand { get; }

    public LoginViewModel(
        IAuthenticationService authenticationService,
        ISettingsService settingsService,
        IUserRepository userRepository)
    {
        _authenticationService = authenticationService;
        _settingsService = settingsService;
        _userRepository = userRepository;

        TogglePasswordVisibilityCommand = new RelayCommand(TogglePasswordVisibility);
        ToggleAdminPasswordVisibilityCommand = new RelayCommand(() => IsAdminPasswordVisible = !IsAdminPasswordVisible);
        ToggleResetPasswordVisibilityCommand = new RelayCommand(() => IsResetPasswordVisible = !IsResetPasswordVisible);
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        ExitCommand = new RelayCommand(Exit);
        OpenForgotPasswordCommand = new RelayCommand(OpenForgotPassword);
        CloseForgotPasswordCommand = new RelayCommand(CloseForgotPassword);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);

        RememberUsername = _settingsService.RememberUsername;
        if (RememberUsername)
            Username = _settingsService.LastUsername;
    }

    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var (success, error) = await _authenticationService.LoginAsync(Username, Password, RememberUsername);
            if (success)
            {
                Password = string.Empty;
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = error;
                Password = string.Empty;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Exit() => ExitRequested?.Invoke();

    private void OpenForgotPassword()
    {
        ShowForgotPassword = true;
        ResetMessage = null;
        SuccessMessage = null;
        ErrorMessage = null;
        AdminPassword = string.Empty;
        ResetNewPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
        IsAdminPasswordVisible = false;
        IsResetPasswordVisible = false;
        ResetPasswordStrength = 0;
        ResetPasswordStrengthLabel = "Very weak";

        LoadResetUsers();

        // Prefer the remembered username when it is an administrator.
        var remembered = _settingsService.LastUsername?.Trim() ?? string.Empty;
        var adminUser = ResetUsers.FirstOrDefault(u =>
            u.IsAdministrator &&
            string.Equals(u.Username, remembered, StringComparison.OrdinalIgnoreCase));
        AdminUsername = adminUser?.Username
                        ?? ResetUsers.FirstOrDefault(u => u.IsAdministrator)?.Username
                        ?? string.Empty;

        SelectedResetUser = ResetUsers.FirstOrDefault(u =>
                                string.Equals(u.Username, Username, StringComparison.OrdinalIgnoreCase))
                            ?? ResetUsers.FirstOrDefault(u => !u.IsAdministrator)
                            ?? ResetUsers.FirstOrDefault();
    }

    private void LoadResetUsers()
    {
        ResetUsers.Clear();
        foreach (var user in _userRepository.GetAll()
                     .Where(u => u.IsActive && !u.IsLocked)
                     .OrderBy(u => u.DisplayName)
                     .ThenBy(u => u.Username))
        {
            ResetUsers.Add(new ResetUserOption(
                user.Username,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
                user.Role == UserRole.Administrator));
        }
    }

    private void CloseForgotPassword()
    {
        ShowForgotPassword = false;
        ResetMessage = null;
        AdminPassword = string.Empty;
        ResetNewPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
    }

    private async Task ResetPasswordAsync()
    {
        ResetMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(AdminUsername))
        {
            ResetMessage = "Enter the administrator username.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AdminPassword))
        {
            ResetMessage = "Enter the administrator password.";
            return;
        }

        if (SelectedResetUser is null)
        {
            ResetMessage = "Choose which user to reset.";
            return;
        }

        if (ResetNewPassword != ResetConfirmPassword)
        {
            ResetMessage = "New passwords do not match.";
            return;
        }

        var passwordError = PasswordRules.Validate(ResetNewPassword);
        if (passwordError is not null)
        {
            ResetMessage = passwordError;
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _authenticationService.AdminResetPasswordAsync(
                AdminUsername, AdminPassword, SelectedResetUser.Username, ResetNewPassword);

            if (success)
            {
                var display = SelectedResetUser.DisplayName;
                Username = SelectedResetUser.Username;
                Password = string.Empty;
                AdminPassword = string.Empty;
                ResetNewPassword = string.Empty;
                ResetConfirmPassword = string.Empty;
                ShowForgotPassword = false;
                ErrorMessage = null;
                SuccessMessage = $"Password updated for {display}. Sign in with the new password.";
            }
            else
            {
                ResetMessage = error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class ResetUserOption(string username, string displayName, bool isAdministrator)
{
    public string Username { get; } = username;
    public string DisplayName { get; } = displayName;
    public bool IsAdministrator { get; } = isAdministrator;

    public string Label => IsAdministrator
        ? $"{DisplayName} ({Username}) — Administrator"
        : $"{DisplayName} ({Username})";

    public override string ToString() => Label;
}
