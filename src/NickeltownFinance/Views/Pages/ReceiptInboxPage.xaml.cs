using System.Windows;
using System.Windows.Input;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Pages;

public partial class ReceiptInboxPage
{
    public ReceiptInboxPage() => InitializeComponent();

    private ReceiptInboxViewModel? Vm => DataContext as ReceiptInboxViewModel;

    private void Preview_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        Vm?.ViewReceiptCommand.Execute(null);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (Vm is null) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        await Vm.ImportPathsAsync(files);
    }
}
