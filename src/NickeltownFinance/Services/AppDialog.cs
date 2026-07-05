using System.Windows;
using NickeltownFinance.Views.Dialogs;

namespace NickeltownFinance.Services;

public static class AppDialog
{
    public static bool Confirm(
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No",
        bool isDanger = false)
    {
        var kind = isDanger ? AppDialogKind.DangerConfirm : AppDialogKind.Confirm;
        return Show(kind, title, message, confirmText, cancelText) == true;
    }

    public static void Info(string title, string message) =>
        Show(AppDialogKind.Information, title, message);

    public static void Success(string title, string message) =>
        Show(AppDialogKind.Success, title, message);

    public static void Warning(string title, string message) =>
        Show(AppDialogKind.Warning, title, message);

    public static void Error(string title, string message) =>
        Show(AppDialogKind.Error, title, message);

    private static bool? Show(
        AppDialogKind kind,
        string title,
        string message,
        string? primaryText = null,
        string? secondaryText = null)
    {
        var viewModel = new AppDialogViewModel(kind, title, message, primaryText, secondaryText);
        var window = new AppDialogWindow(viewModel);

        var owner = ResolveOwner();
        if (owner is not null && owner.IsLoaded)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window.ShowDialog();
    }

    private static Window? ResolveOwner()
    {
        if (Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) is { } active)
            return active;

        return Application.Current?.MainWindow is { IsLoaded: true } main ? main : null;
    }
}
