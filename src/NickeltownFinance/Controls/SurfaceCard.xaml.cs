using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace NickeltownFinance.Controls;

/// <summary>
/// Shared elevated surface used by settings panels, report filters, and other card layouts.
/// </summary>
[ContentProperty(nameof(Body))]
public partial class SurfaceCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SurfaceCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(SurfaceCard));

    public static readonly DependencyProperty CardPaddingProperty =
        DependencyProperty.Register(nameof(CardPadding), typeof(Thickness), typeof(SurfaceCard),
            new PropertyMetadata(new Thickness(16)));

    public static readonly DependencyProperty CardMarginProperty =
        DependencyProperty.Register(nameof(CardMargin), typeof(Thickness), typeof(SurfaceCard),
            new PropertyMetadata(new Thickness(0, 0, 0, 12)));

    public SurfaceCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public Thickness CardPadding
    {
        get => (Thickness)GetValue(CardPaddingProperty);
        set => SetValue(CardPaddingProperty, value);
    }

    public Thickness CardMargin
    {
        get => (Thickness)GetValue(CardMarginProperty);
        set => SetValue(CardMarginProperty, value);
    }
}
