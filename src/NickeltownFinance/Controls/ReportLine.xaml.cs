using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace NickeltownFinance.Controls;

public partial class ReportLine : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ReportLine),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AmountProperty =
        DependencyProperty.Register(nameof(Amount), typeof(decimal?), typeof(ReportLine),
            new PropertyMetadata(null, OnAmountChanged));

    public static readonly DependencyProperty AmountTextProperty =
        DependencyProperty.Register(nameof(AmountText), typeof(string), typeof(ReportLine),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsBoldProperty =
        DependencyProperty.Register(nameof(IsBold), typeof(bool), typeof(ReportLine),
            new PropertyMetadata(false, OnIsBoldChanged));

    public static readonly DependencyProperty LabelFontWeightProperty =
        DependencyProperty.Register(nameof(LabelFontWeight), typeof(FontWeight), typeof(ReportLine),
            new PropertyMetadata(FontWeights.Normal));

    public static readonly DependencyProperty LineMarginProperty =
        DependencyProperty.Register(nameof(LineMargin), typeof(Thickness), typeof(ReportLine),
            new PropertyMetadata(new Thickness(0, 2, 0, 2)));

    public ReportLine()
    {
        InitializeComponent();
        UpdateFontWeight();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public decimal? Amount
    {
        get => (decimal?)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    public string AmountText
    {
        get => (string)GetValue(AmountTextProperty);
        set => SetValue(AmountTextProperty, value);
    }

    public bool IsBold
    {
        get => (bool)GetValue(IsBoldProperty);
        set => SetValue(IsBoldProperty, value);
    }

    public FontWeight LabelFontWeight
    {
        get => (FontWeight)GetValue(LabelFontWeightProperty);
        private set => SetValue(LabelFontWeightProperty, value);
    }

    public Thickness LineMargin
    {
        get => (Thickness)GetValue(LineMarginProperty);
        set => SetValue(LineMarginProperty, value);
    }

    private static void OnAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReportLine line)
            return;

        if (e.NewValue is decimal amount)
            line.AmountText = amount.ToString("C", CultureInfo.CurrentCulture);
    }

    private static void OnIsBoldChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReportLine line)
            line.UpdateFontWeight();
    }

    private void UpdateFontWeight() =>
        LabelFontWeight = IsBold ? FontWeights.Bold : FontWeights.Normal;
}
