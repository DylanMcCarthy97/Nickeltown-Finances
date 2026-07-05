using CommunityToolkit.Mvvm.ComponentModel;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.ViewModels;

public partial class SquareDepositRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _depositId = string.Empty;
    [ObservableProperty] private DateTime _depositDate;
    [ObservableProperty] private decimal _grossAmount;
    [ObservableProperty] private decimal _fees;
    [ObservableProperty] private decimal _netAmount;
    [ObservableProperty] private int _transactionCount;
    [ObservableProperty] private ImportRowStatus _status = ImportRowStatus.New;
    [ObservableProperty] private string _fingerprint = string.Empty;

    public IReadOnlyList<Core.DTOs.SquareTransactionPreviewLine> Lines { get; set; } = [];

    public string StatusDisplay => Status.ToString();
}
