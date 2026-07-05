using System.Windows;
using System.Windows.Input;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views;

public partial class LoginWindow
{
    private readonly LoginViewModel _viewModel;
    private bool _syncingPassword;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.LoginSucceeded += OnLoginSucceeded;
        viewModel.ExitRequested += OnExitRequested;
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += (_, _) => UsernameFocus();
    }

    private void UsernameFocus()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Username))
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        else
            PasswordBox.Focus();
    }

    private void ViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.Password) && !_viewModel.IsPasswordVisible)
            SyncPasswordBox(PasswordBox, _viewModel.Password);

        if (e.PropertyName == nameof(LoginViewModel.AdminPassword) && !_viewModel.IsAdminPasswordVisible)
            SyncPasswordBox(AdminPasswordBox, _viewModel.AdminPassword);

        if (e.PropertyName == nameof(LoginViewModel.ResetNewPassword) && !_viewModel.IsResetPasswordVisible)
            SyncPasswordBox(ResetNewPasswordBox, _viewModel.ResetNewPassword);

        if (e.PropertyName == nameof(LoginViewModel.ResetConfirmPassword) && !_viewModel.IsResetPasswordVisible)
            SyncPasswordBox(ResetConfirmPasswordBox, _viewModel.ResetConfirmPassword);

        if (e.PropertyName == nameof(LoginViewModel.IsPasswordVisible))
        {
            if (_viewModel.IsPasswordVisible)
                VisiblePasswordBox.Focus();
            else
                PasswordBox.Focus();
        }

        if (e.PropertyName == nameof(LoginViewModel.ShowForgotPassword) && _viewModel.ShowForgotPassword)
        {
            SyncPasswordBox(AdminPasswordBox, string.Empty);
            SyncPasswordBox(ResetNewPasswordBox, string.Empty);
            SyncPasswordBox(ResetConfirmPasswordBox, string.Empty);
        }

        if (e.PropertyName == nameof(LoginViewModel.ShowForgotPassword) && !_viewModel.ShowForgotPassword)
            PasswordBox.Focus();
    }

    private void SyncPasswordBox(System.Windows.Controls.PasswordBox box, string value)
    {
        _syncingPassword = true;
        if (box.Password != value)
            box.Password = value;
        _syncingPassword = false;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _viewModel.Password = PasswordBox.Password;
    }

    private void PasswordBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _viewModel.LoginCommand.Execute(null);
    }

    private void AdminPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _viewModel.AdminPassword = AdminPasswordBox.Password;
    }

    private void ResetNewPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _viewModel.ResetNewPassword = ResetNewPasswordBox.Password;
    }

    private void ResetConfirmPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _viewModel.ResetConfirmPassword = ResetConfirmPasswordBox.Password;
    }

    private void OnLoginSucceeded()
    {
        DialogResult = true;
        Close();
    }

    private void OnExitRequested()
    {
        DialogResult = false;
        Close();
    }
}
