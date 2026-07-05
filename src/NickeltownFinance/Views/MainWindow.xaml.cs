using System.ComponentModel;
using System.Windows;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NickeltownFinance.Views;

public partial class MainWindow : FluentWindow
{
    private readonly IWindowStateService _windowStateService;

    public MainWindow(
        MainViewModel viewModel,
        NotificationService notificationService,
        IWindowStateService windowStateService)
    {
        _windowStateService = windowStateService;
        InitializeComponent();
        DataContext = viewModel;

        var snackbar = new SnackbarService();
        snackbar.SetSnackbarPresenter(NotificationHost.PresenterControl);
        notificationService.SetHost(snackbar);


        _windowStateService.Restore(this);
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnClosing(object? sender, CancelEventArgs e) =>
        _windowStateService.Save(this);

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel main)
            main.Detach();
    }
}
