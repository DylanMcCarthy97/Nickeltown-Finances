using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NickeltownFinance.Controls;

public partial class StatisticRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatisticRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatisticRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(MoneyTone), typeof(StatisticRow),
            new PropertyMetadata(MoneyTone.Neutral, OnToneChanged));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(StatisticRow),
            new PropertyMetadata(SystemColors.ControlTextBrush));

    public static readonly DependencyProperty IsEmphasizedProperty =
        DependencyProperty.Register(nameof(IsEmphasized), typeof(bool), typeof(StatisticRow),
            new PropertyMetadata(false, OnIsEmphasizedChanged));

    public static readonly DependencyProperty ValueFontWeightProperty =
        DependencyProperty.Register(nameof(ValueFontWeight), typeof(FontWeight), typeof(StatisticRow),
            new PropertyMetadata(FontWeights.Normal));

    public StatisticRow()
    {
        InitializeComponent();
        UpdateAppearance();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public MoneyTone Tone
    {
        get => (MoneyTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public Brush ValueBrush
    {
        get => (Brush)GetValue(ValueBrushProperty);
        private set => SetValue(ValueBrushProperty, value);
    }

    public bool IsEmphasized
    {
        get => (bool)GetValue(IsEmphasizedProperty);
        set => SetValue(IsEmphasizedProperty, value);
    }

    public FontWeight ValueFontWeight
    {
        get => (FontWeight)GetValue(ValueFontWeightProperty);
        private set => SetValue(ValueFontWeightProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatisticRow row)
            row.UpdateAppearance();
    }

    private static void OnIsEmphasizedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatisticRow row)
            row.UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        ValueFontWeight = IsEmphasized ? FontWeights.SemiBold : FontWeights.Normal;
        ValueBrush = Tone switch
        {
            MoneyTone.Income => ResolveBrush("BrandIncomeBrush", "#2F8F74"),
            MoneyTone.Expense => ResolveBrush("BrandExpenseBrush", "#C45C4A"),
            MoneyTone.Profit => ResolveBrush("BrandProfitBrush", "#3D7EA6"),
            _ => SystemColors.ControlTextBrush
        };
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
