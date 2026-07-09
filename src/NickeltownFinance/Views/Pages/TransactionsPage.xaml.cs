using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Pages;

public partial class TransactionsPage : UserControl
{
    public TransactionsPage() => InitializeComponent();

    private void DataGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not TransactionsViewModel vm || vm.SelectedTransaction is null)
            return;

        if (vm.SelectedTransaction.HasSquareDepositDetail || vm.SelectedTransaction.IsSquareAwaitingMatch)
            return;

        if (vm.SelectedTransaction.HasReceipt)
            vm.OpenReceiptViewerForSelected();
        else
            vm.EditSelectedTransaction();
    }

    private void ReceiptIcon_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransactionListItem item } &&
            DataContext is TransactionsViewModel vm)
        {
            vm.SelectedTransaction = item;
            vm.ViewReceiptsCommand.Execute(null);
        }
    }

    private void TransactionsGrid_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void TransactionsGrid_OnDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not TransactionsViewModel vm) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var element = e.OriginalSource as DependencyObject;
            while (element is not null && element is not DataGridRow)
                element = System.Windows.Media.VisualTreeHelper.GetParent(element);

            if (element is not DataGridRow { Item: TransactionListItem item })
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            await vm.DropFilesOnTransactionAsync(item, files);
        }
        catch (Exception ex)
        {
            if (DataContext is TransactionsViewModel vm)
                vm.ErrorMessage = ex.Message;
        }
    }
}
