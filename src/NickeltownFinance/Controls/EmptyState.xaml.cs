using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Wpf.Ui.Controls;

namespace NickeltownFinance.Controls;

[ContentProperty(nameof(ActionContent))]
public partial class EmptyState : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyState),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconSymbolProperty =
        DependencyProperty.Register(nameof(IconSymbol), typeof(SymbolRegular), typeof(EmptyState),
            new PropertyMetadata(SymbolRegular.DocumentSearch24));

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(EmptyState));

    public EmptyState() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public SymbolRegular IconSymbol
    {
        get => (SymbolRegular)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
}
