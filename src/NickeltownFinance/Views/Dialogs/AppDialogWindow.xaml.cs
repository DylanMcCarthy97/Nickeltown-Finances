using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;

namespace NickeltownFinance.Views.Dialogs;

public partial class AppDialogWindow
{
    public AppDialogWindow(AppDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}

public enum AppDialogKind
{
    Information,
    Success,
    Warning,
    Error,
    Confirm,
    DangerConfirm
}

public partial class AppDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _titleText = string.Empty;
    [ObservableProperty] private string _messageText = string.Empty;
    [ObservableProperty] private string _primaryText = "OK";
    [ObservableProperty] private string _secondaryText = "Cancel";
    [ObservableProperty] private bool _showSecondary;
    [ObservableProperty] private SymbolRegular _iconSymbol = SymbolRegular.Info24;
    [ObservableProperty] private Brush _iconBackground = Brushes.Transparent;
    [ObservableProperty] private Brush _iconForeground = Brushes.White;
    [ObservableProperty] private ControlAppearance _primaryAppearance = ControlAppearance.Primary;

    public event Action<bool>? CloseRequested;

    public AppDialogViewModel(AppDialogKind kind, string title, string message, string? primaryText = null, string? secondaryText = null)
    {
        TitleText = title;
        MessageText = message;
        ApplyKind(kind, primaryText, secondaryText);
    }

    private void ApplyKind(AppDialogKind kind, string? primaryText, string? secondaryText)
    {
        switch (kind)
        {
            case AppDialogKind.Information:
                IconSymbol = SymbolRegular.Info24;
                IconBackground = BrushFrom("#1A60CDFF");
                IconForeground = BrushFrom("#60CDFF");
                PrimaryText = primaryText ?? "OK";
                PrimaryAppearance = ControlAppearance.Primary;
                ShowSecondary = false;
                break;

            case AppDialogKind.Success:
                IconSymbol = SymbolRegular.CheckmarkCircle24;
                IconBackground = BrushFrom("#1A6CCB5F");
                IconForeground = BrushFrom("#6CCB5F");
                PrimaryText = primaryText ?? "OK";
                PrimaryAppearance = ControlAppearance.Success;
                ShowSecondary = false;
                break;

            case AppDialogKind.Warning:
                IconSymbol = SymbolRegular.Warning24;
                IconBackground = BrushFrom("#1AFCE100");
                IconForeground = BrushFrom("#FCE100");
                PrimaryText = primaryText ?? "OK";
                PrimaryAppearance = ControlAppearance.Caution;
                ShowSecondary = false;
                break;

            case AppDialogKind.Error:
                IconSymbol = SymbolRegular.ErrorCircle24;
                IconBackground = BrushFrom("#1AE81123");
                IconForeground = BrushFrom("#FF99A4");
                PrimaryText = primaryText ?? "OK";
                PrimaryAppearance = ControlAppearance.Danger;
                ShowSecondary = false;
                break;

            case AppDialogKind.Confirm:
                IconSymbol = SymbolRegular.QuestionCircle24;
                IconBackground = BrushFrom("#1A60CDFF");
                IconForeground = BrushFrom("#60CDFF");
                PrimaryText = primaryText ?? "Yes";
                SecondaryText = secondaryText ?? "No";
                PrimaryAppearance = ControlAppearance.Primary;
                ShowSecondary = true;
                break;

            case AppDialogKind.DangerConfirm:
                IconSymbol = SymbolRegular.Delete24;
                IconBackground = BrushFrom("#1AE81123");
                IconForeground = BrushFrom("#FF99A4");
                PrimaryText = primaryText ?? "Delete";
                SecondaryText = secondaryText ?? "Cancel";
                PrimaryAppearance = ControlAppearance.Danger;
                ShowSecondary = true;
                break;
        }
    }

    [RelayCommand]
    private void Primary() => CloseRequested?.Invoke(true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
