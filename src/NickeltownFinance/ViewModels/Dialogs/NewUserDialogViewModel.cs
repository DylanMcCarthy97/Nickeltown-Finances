using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class NewUserDialogViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private bool _usernameManuallyEdited;
    private bool _syncingUsername;

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private UserRole _selectedRole = UserRole.Treasurer;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private int _passwordStrength;
    [ObservableProperty] private string _passwordStrengthLabel = "Very weak";

    public UserRole[] Roles { get; } =
    [
        UserRole.Administrator,
        UserRole.Treasurer,
        UserRole.Committee,
        UserRole.ReadOnly
    ];

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public NewUserDialogViewModel(IUserService userService) => _userService = userService;

    partial void OnFullNameChanged(string value)
    {
        if (_usernameManuallyEdited)
            return;

        _syncingUsername = true;
        try
        {
            Username = BuildUniqueUsername(value);
        }
        finally
        {
            _syncingUsername = false;
        }
    }

    partial void OnUsernameChanged(string value)
    {
        if (!_syncingUsername)
            _usernameManuallyEdited = true;
    }

    partial void OnPasswordChanged(string value)
    {
        PasswordStrength = PasswordRules.GetStrength(value);
        PasswordStrengthLabel = PasswordRules.GetStrengthLabel(PasswordStrength);
    }

    [RelayCommand]
    private void RegenerateUsername()
    {
        _usernameManuallyEdited = false;
        _syncingUsername = true;
        try
        {
            Username = BuildUniqueUsername(FullName);
        }
        finally
        {
            _syncingUsername = false;
        }
    }

    private string BuildUniqueUsername(string fullName)
    {
        var preferred = UsernameGenerator.FromFullName(fullName);
        if (string.IsNullOrWhiteSpace(preferred))
            return string.Empty;

        return UsernameGenerator.EnsureUnique(preferred, _userService.IsUsernameAvailable);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Full name is required.";
            return;
        }

        if (FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            ErrorMessage = "Enter the person's full name (first and last name).";
            return;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            Username = BuildUniqueUsername(FullName);
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Could not generate a username from that name.";
                return;
            }
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords must match.";
            return;
        }

        var passwordError = PasswordRules.Validate(Password);
        if (passwordError is not null)
        {
            ErrorMessage = passwordError;
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _userService.CreateAsync(
                Username.Trim().ToLowerInvariant(),
                FullName.Trim(),
                Password,
                SelectedRole,
                IsActive);

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
