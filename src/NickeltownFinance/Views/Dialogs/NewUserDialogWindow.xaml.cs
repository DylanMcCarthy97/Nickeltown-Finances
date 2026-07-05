using NickeltownFinance.ViewModels.Dialogs;

namespace NickeltownFinance.Views.Dialogs;

public partial class NewUserDialogWindow
{
    public NewUserDialogWindow(NewUserDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = viewModel.DialogResult;
    }
}
