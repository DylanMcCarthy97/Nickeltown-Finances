using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views;

public partial class SetupWizardWindow
{
    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) =>
        {
            DialogResult = viewModel.DialogResult;
            Close();
        };
    }
}
