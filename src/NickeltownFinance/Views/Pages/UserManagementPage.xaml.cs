using System.Windows.Controls;
using System.Windows.Input;
using NickeltownFinance.ViewModels;

namespace NickeltownFinance.Views.Pages;

public partial class UserManagementPage : UserControl
{
    public UserManagementPage() => InitializeComponent();

    private void UsersGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.OpenUserCommand.Execute(null);
    }

    private void Avatar_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.BrowseProfilePictureCommand.Execute(null);
    }
}
