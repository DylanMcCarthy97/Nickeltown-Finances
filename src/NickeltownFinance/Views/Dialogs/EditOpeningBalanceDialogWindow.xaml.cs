using NickeltownFinance.ViewModels.Dialogs;

namespace NickeltownFinance.Views.Dialogs;

public partial class EditOpeningBalanceDialogWindow
{
    public EditOpeningBalanceDialogWindow(EditOpeningBalanceDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = viewModel.DialogResult;
    }
}
