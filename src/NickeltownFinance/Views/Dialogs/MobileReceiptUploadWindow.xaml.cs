using System.ComponentModel;
using System.Windows;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.ViewModels.Dialogs;

namespace NickeltownFinance.Views.Dialogs;

public partial class MobileReceiptUploadWindow
{
    private readonly MobileReceiptUploadViewModel _viewModel;
    private readonly MobileUploadSessionRequest? _sessionRequest;

    public MobileReceiptUploadWindow(MobileReceiptUploadViewModel viewModel, MobileUploadSessionRequest? sessionRequest = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _sessionRequest = sessionRequest;
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsInitialized)
            return;

        await _viewModel.InitializeAsync(_sessionRequest);
    }

    private void OnRequestClose(object? sender, bool _) =>
        Close();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        await _viewModel.StopSessionSilentlyAsync();
    }
}
