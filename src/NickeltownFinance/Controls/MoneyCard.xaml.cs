using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace NickeltownFinance.Controls;

public partial class MoneyCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MoneyCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AmountProperty =
        DependencyProperty.Register(nameof(Amount), typeof(decimal), typeof(MoneyCard),
            new PropertyMetadata(0m));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(MoneyTone), typeof(MoneyCard),
            new PropertyMetadata(MoneyTone.Neutral, OnToneChanged));

    public static readonly DependencyProperty AmountBrushProperty =
        DependencyProperty.Register(nameof(AmountBrush), typeof(Brush), typeof(MoneyCard),
            new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(MoneyCard),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty AccentFillBrushProperty =
        DependencyProperty.Register(nameof(AccentFillBrush), typeof(Brush), typeof(MoneyCard),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty IconSymbolProperty =
        DependencyProperty.Register(nameof(IconSymbol), typeof(SymbolRegular), typeof(MoneyCard),
            new PropertyMetadata(SymbolRegular.Money24));

    public static readonly DependencyProperty ShowIconProperty =
        DependencyProperty.Register(nameof(ShowIcon), typeof(bool), typeof(MoneyCard),
            new PropertyMetadata(true));

    public MoneyCard()
    {
        InitializeComponent();
        UpdateToneVisuals();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public decimal Amount
    {
        get => (decimal)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    public MoneyTone Tone
    {
        get => (MoneyTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public Brush AmountBrush
    {
        get => (Brush)GetValue(AmountBrushProperty);
        private set => SetValue(AmountBrushProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        private set => SetValue(AccentBrushProperty, value);
    }

    public Brush AccentFillBrush
    {
        get => (Brush)GetValue(AccentFillBrushProperty);
        private set => SetValue(AccentFillBrushProperty, value);
    }

    public SymbolRegular IconSymbol
    {
        get => (SymbolRegular)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MoneyCard card)
            card.UpdateToneVisuals();
    }

    private void UpdateToneVisuals()
    {
        switch (Tone)
        {
            case MoneyTone.Income:
                AmountBrush = ResolveBrush("BrandIncomeBrush", "#22C55E");
                AccentBrush = AmountBrush;
                AccentFillBrush = ResolveBrush("BrandIncomeFillBrush", "#2922C55E");
                IconSymbol = SymbolRegular.ArrowUp24;
                break;
            case MoneyTone.Expense:
                AmountBrush = ResolveBrush("BrandExpenseBrush", "#EF4444");
                AccentBrush = AmountBrush;
                AccentFillBrush = ResolveBrush("BrandExpenseFillBrush", "#29EF4444");
                IconSymbol = SymbolRegular.ArrowDown24;
                break;
            case MoneyTone.Profit:
                AmountBrush = ResolveBrush("BrandProfitBrush", "#2D8CFF");
                AccentBrush = AmountBrush;
                AccentFillBrush = ResolveBrush("BrandProfitFillBrush", "#292D8CFF");
                IconSymbol = SymbolRegular.DataTrending24;
                break;
            default:
                AmountBrush = ResolveBrush("BrandBalanceBrush", "#2D8CFF");
                AccentBrush = AmountBrush;
                AccentFillBrush = ResolveBrush("BrandBalanceFillBrush", "#292D8CFF");
                IconSymbol = SymbolRegular.Wallet24;
                break;
        }
    }

    private static Brush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;

        var fallback = (SolidColorBrush)new BrushConverter().ConvertFrom(fallbackHex)!;
        fallback.Freeze();
        return fallback;
    }
}
