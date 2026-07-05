using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace NickeltownFinance.Controls;

/// <summary>
/// Host for design-system success, error, and info notifications (snackbars).
/// </summary>
public partial class SuccessNotificationHost : UserControl
{
    public SuccessNotificationHost() => InitializeComponent();

    public SnackbarPresenter PresenterControl => Presenter;
}
