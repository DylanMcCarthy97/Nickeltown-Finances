using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownFinance.Core.DTOs;

namespace NickeltownFinance.ViewModels;

public partial class SquareDepositGroupViewModel : ObservableObject
{
    public SquareDepositGroupViewModel(SquareDepositItemGroupDto group)
    {
        Description = group.Description;
        DisplayTitle = group.DisplayTitle;
        TotalAmount = group.TotalAmount;
        Items = new ObservableCollection<SquareDepositLineItemDto>(group.Items);
    }

    public string Description { get; }

    public string DisplayTitle { get; }

    public decimal TotalAmount { get; }

    [ObservableProperty] private bool _isExpanded;

    public ObservableCollection<SquareDepositLineItemDto> Items { get; }
}
