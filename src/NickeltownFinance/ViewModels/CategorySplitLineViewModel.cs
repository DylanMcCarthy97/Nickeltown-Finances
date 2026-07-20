using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.ViewModels;

public partial class CategorySplitLineViewModel : ObservableObject
{
    [ObservableProperty] private Category? _selectedCategory;

    public CategorySplitLineViewModel()
    {
    }

    public CategorySplitLineViewModel(Category? category)
    {
        SelectedCategory = category;
    }
}
