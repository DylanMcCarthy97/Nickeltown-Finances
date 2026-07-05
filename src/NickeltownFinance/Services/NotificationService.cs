using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NickeltownFinance.Services;

/// <summary>
/// Design-system success, error, and info notifications. Always use this instead of ad-hoc toasts.
/// </summary>
public interface INotificationService
{
    void ShowSuccess(string message);

    void ShowError(string message);

    void ShowInfo(string message);
}

public class NotificationService : INotificationService
{
    private ISnackbarService? _snackbar;

    public void SetHost(ISnackbarService snackbar) => _snackbar = snackbar;

    public void ShowSuccess(string message) =>
        _snackbar?.Show("Success", message, ControlAppearance.Success, null, TimeSpan.FromSeconds(4));

    public void ShowError(string message) =>
        _snackbar?.Show("Error", message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(5));

    public void ShowInfo(string message) =>
        _snackbar?.Show("Info", message, ControlAppearance.Info, null, TimeSpan.FromSeconds(4));
}
