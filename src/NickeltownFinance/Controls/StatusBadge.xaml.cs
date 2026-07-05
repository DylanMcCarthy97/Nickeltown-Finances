using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NickeltownFinance.Controls;

public partial class StatusBadge : UserControl
{
    public enum BadgeTone
    {
        Neutral,
        Success,
        Warning,
        Danger,
        Info
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(BadgeTone), typeof(StatusBadge),
            new PropertyMetadata(BadgeTone.Neutral, OnToneChanged));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(StatusBadge));

    public static readonly DependencyProperty BadgeForegroundProperty =
        DependencyProperty.Register(nameof(BadgeForeground), typeof(Brush), typeof(StatusBadge));

    public StatusBadge()
    {
        InitializeComponent();
        UpdateTone();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeTone Tone
    {
        get => (BadgeTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public Brush BadgeBackground
    {
        get => (Brush)GetValue(BadgeBackgroundProperty);
        private set => SetValue(BadgeBackgroundProperty, value);
    }

    public Brush BadgeForeground
    {
        get => (Brush)GetValue(BadgeForegroundProperty);
        private set => SetValue(BadgeForegroundProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge)
            badge.UpdateTone();
    }

    private void UpdateTone()
    {
        (BadgeBackground, BadgeForeground) = Tone switch
        {
            BadgeTone.Success => (Brush("BrandSuccessFillBrush"), Brush("BrandSuccessBrush")),
            BadgeTone.Warning => (Brush("BrandWarningFillBrush"), Brush("BrandWarningBrush")),
            BadgeTone.Danger => (Brush("BrandDangerFillBrush"), Brush("BrandDangerBrush")),
            BadgeTone.Info => (Brush("BrandAccentFillBrush"), Brush("BrandPrimaryBrush")),
            _ => (Brush("BrandElevatedBrush"), Brush("BrandTextSecondaryBrush"))
        };
    }

    private static Brush Brush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return Brushes.Gray;
    }
}
