using NickeltownFinance.ViewModels.Dialogs;

namespace NickeltownFinance.Views.Dialogs;

public partial class ManualPitstopEntryDialogWindow
{
    public ManualPitstopEntryDialogWindow(ManualPitstopEntryDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = viewModel.DialogResult;
    }
}
