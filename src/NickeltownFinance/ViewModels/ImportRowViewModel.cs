using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.ViewModels;

public partial class ImportRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private decimal _debit;
    [ObservableProperty] private decimal _credit;
    [ObservableProperty] private decimal? _balance;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private ImportRowStatus _status = ImportRowStatus.New;
    [ObservableProperty] private ObjectId? _matchedTransactionId;
    [ObservableProperty] private string _fingerprint = string.Empty;
    [ObservableProperty] private double _duplicateConfidence;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isSquareDeposit;
    [ObservableProperty] private bool _rememberCategory;

    public ObservableCollection<Category> Categories { get; set; } = [];

    public string CategoryDisplay =>
        SelectedCategory?.Name
        ?? (IsSquareDeposit ? "Needs category" : null)
        ?? (Status == ImportRowStatus.NeedsReview ? "Needs Review" : string.Empty);

    public string BalanceDisplay => Balance?.ToString("C") ?? "—";

    partial void OnSelectedCategoryChanged(Category? value)
    {
        OnPropertyChanged(nameof(CategoryDisplay));
        if (value is not null && Status == ImportRowStatus.NeedsReview)
        {
            Status = ImportRowStatus.New;
            RememberCategory = true;
        }
    }

    partial void OnIsSquareDepositChanged(bool value) => OnPropertyChanged(nameof(CategoryDisplay));

    partial void OnBalanceChanged(decimal? value) => OnPropertyChanged(nameof(BalanceDisplay));

    public decimal Amount => Credit > 0 ? Credit : Debit;

    public string TypeDisplay => Credit > 0 ? "Income" : "Expense";

    public string AmountDisplay => Amount.ToString("C");

    public string StatusDisplay => Status switch
    {
        ImportRowStatus.NeedsReview => "Needs Review",
        ImportRowStatus.New => "Ready",
        ImportRowStatus.Duplicate => "Duplicate",
        ImportRowStatus.Matched => "Matched",
        ImportRowStatus.Ignored => "Ignored",
        _ => Status.ToString()
    };

    public Brush StatusBrush => Status switch
    {
        ImportRowStatus.New => BrushFrom("#1A2E7D32"),
        ImportRowStatus.NeedsReview => BrushFrom("#1AF9A825"),
        ImportRowStatus.Duplicate => BrushFrom("#1AF9A825"),
        ImportRowStatus.Matched => BrushFrom("#1AE53935"),
        ImportRowStatus.Ignored => BrushFrom("#1A9E9E9E"),
        _ => Brushes.Transparent
    };

    public Brush StatusTextBrush => Status switch
    {
        ImportRowStatus.New => BrushFrom("#81C784"),
        ImportRowStatus.NeedsReview => BrushFrom("#FFD54F"),
        ImportRowStatus.Duplicate => BrushFrom("#FFD54F"),
        ImportRowStatus.Matched => BrushFrom("#EF9A9A"),
        ImportRowStatus.Ignored => BrushFrom("#BDBDBD"),
        _ => Brushes.White
    };

    public bool PassesFilter(string? search, string? statusFilter, string? typeFilter)
    {
        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All" &&
            !string.Equals(StatusDisplay, statusFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "All" &&
            !string.Equals(TypeDisplay, typeFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(search))
            return true;

        var s = search.Trim();
        return Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
               Notes.Contains(s, StringComparison.OrdinalIgnoreCase) ||
               Reference.Contains(s, StringComparison.OrdinalIgnoreCase) ||
               (SelectedCategory?.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
               StatusDisplay.Contains(s, StringComparison.OrdinalIgnoreCase) ||
               TypeDisplay.Contains(s, StringComparison.OrdinalIgnoreCase) ||
               AmountDisplay.Contains(s, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnStatusChanged(ImportRowStatus value)
    {
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(StatusTextBrush));
    }

    partial void OnCreditChanged(decimal value)
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountDisplay));
        OnPropertyChanged(nameof(TypeDisplay));
    }

    partial void OnDebitChanged(decimal value)
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountDisplay));
        OnPropertyChanged(nameof(TypeDisplay));
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
