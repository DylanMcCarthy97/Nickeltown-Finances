using NickeltownFinance.ViewModels.Dialogs;

namespace NickeltownFinance.Views.Dialogs;

public partial class NewFinancialYearDialogWindow
{
    public NewFinancialYearDialogWindow(NewFinancialYearDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = viewModel.DialogResult;
    }
}
