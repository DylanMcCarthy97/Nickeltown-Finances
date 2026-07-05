using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Dialogs;

public partial class TransactionEditorWindow
{
    public TransactionEditorWindow(TransactionEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
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
        if (DataContext is not TransactionEditorViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var file in files)
            await vm.AddFileAsync(file);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        if (DataContext is not TransactionEditorViewModel vm) return;

        if (Clipboard.ContainsFileDropList())
        {
            foreach (var file in Clipboard.GetFileDropList())
            {
                if (!string.IsNullOrWhiteSpace(file))
                    await vm.AddFileAsync(file);
            }

            e.Handled = true;
            return;
        }

        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is null) return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            await vm.AddBytesAsync(ms.ToArray(), $"paste_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            e.Handled = true;
        }
    }

    private void Attachments_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TransactionEditorViewModel vm)
            vm.ViewAttachmentCommand.Execute(null);
    }
}
