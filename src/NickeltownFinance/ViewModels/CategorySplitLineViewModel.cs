using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.ViewModels;

public partial class CategorySplitLineViewModel : ObservableObject
{
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private decimal _amount;

    public CategorySplitLineViewModel()
    {
    }

    public CategorySplitLineViewModel(Category? category, decimal amount)
    {
        SelectedCategory = category;
        Amount = amount;
    }
}