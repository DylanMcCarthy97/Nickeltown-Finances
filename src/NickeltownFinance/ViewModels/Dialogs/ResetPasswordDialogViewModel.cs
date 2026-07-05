using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Security;

namespace NickeltownFinance.ViewModels.Dialogs;

public partial class ResetPasswordDialogViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private ObjectId _targetUserId = ObjectId.Empty;

    [ObservableProperty] private string _targetDisplay = string.Empty;
    [ObservableProperty] private string _adminPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private int _passwordStrength;
    [ObservableProperty] private string _passwordStrengthLabel = "Very weak";

    public bool DialogResult { get; private set; }

    public event EventHandler? RequestClose;

    public ResetPasswordDialogViewModel(IAuthenticationService authenticationService) =>
        _authenticationService = authenticationService;

    public void Initialize(ObjectId targetUserId, string username, string displayName)
    {
        _targetUserId = targetUserId;
        TargetDisplay = string.IsNullOrWhiteSpace(displayName)
            ? username
            : $"{displayName} ({username})";
        AdminPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        PasswordStrength = 0;
        PasswordStrengthLabel = "Very weak";
        ErrorMessage = null;
        DialogResult = false;
    }

    partial void OnNewPasswordChanged(string value)
    {
        PasswordStrength = PasswordRules.GetStrength(value);
        PasswordStrengthLabel = PasswordRules.GetStrengthLabel(PasswordStrength);
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(AdminPassword))
        {
            ErrorMessage = "Enter your administrator password.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords must match.";
            return;
        }

        var passwordError = PasswordRules.Validate(NewPassword);
        if (passwordError is not null)
        {
            ErrorMessage = passwordError;
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _authenticationService.ResetUserPasswordAsync(
                _targetUserId, AdminPassword, NewPassword);

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
