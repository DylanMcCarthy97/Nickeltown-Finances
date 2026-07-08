using System.Windows;
using System.Windows.Input;
using NickeltownFinance.Services;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Pages;

public partial class ImportPage
{
    private static readonly HashSet<string> ReceiptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    public ImportPage() => InitializeComponent();

    private ImportViewModel? Vm => DataContext as ImportViewModel;

    private void OnReceiptInboxCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.ShowReceiptInboxCommand.Execute(null);

    private void OnMobileUploadCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.OpenMobileReceiptUploadCommand.Execute(null);

    private void OnBankCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.StartAnzImportCommand.Execute(null);

    private void OnLegacyReportCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.StartLegacyImportCommand.Execute(null);

    private void OnHistoryCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.ShowHistoryCommand.Execute(null);

    private void OnRulesCardClick(object sender, MouseButtonEventArgs e) =>
        Vm?.ShowRulesCommand.Execute(null);

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
        var bankFile = files.FirstOrDefault(f =>
            f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
        var receiptFiles = files.Where(f => ReceiptExtensions.Contains(Path.GetExtension(f))).ToList();

        if (receiptFiles.Count > 0)
        {
            await Vm.ImportReceiptFilesAsync(receiptFiles);
            return;
        }

        if (bankFile is null)
        {
            AppDialog.Info("Import", "Drop an ANZ CSV/Excel statement or receipt images/PDFs.");
            return;
        }

        Vm.StartAnzImportCommand.Execute(null);
        await Vm.AnalyseFileAsync(bankFile);
    }
}
