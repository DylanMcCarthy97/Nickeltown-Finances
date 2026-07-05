using System.Windows;
using System.Windows.Input;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Dialogs;

public partial class ReceiptViewerWindow
{
    private Point _dragStart;
    private bool _isDragging;

    public ReceiptViewerWindow(ReceiptViewerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }

    private ReceiptViewerViewModel ViewModel => (ReceiptViewerViewModel)DataContext;

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.None)
        {
            ViewModel.AdjustZoom(e.Delta > 0 ? 0.1 : -0.1);
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
            return;

        _dragStart = e.GetPosition(this);
        _isDragging = true;
        CaptureMouse();
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        ViewModel.PanBy(current.X - _dragStart.X, current.Y - _dragStart.Y);
        _dragStart = current;
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        ReleaseMouseCapture();
    }
}
