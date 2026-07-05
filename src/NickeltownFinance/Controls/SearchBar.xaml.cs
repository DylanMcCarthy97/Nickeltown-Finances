using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NickeltownFinance.Controls;

public partial class SearchBar : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchBar),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SearchBar),
            new PropertyMetadata("Search..."));

    public static readonly DependencyProperty SearchCommandProperty =
        DependencyProperty.Register(nameof(SearchCommand), typeof(ICommand), typeof(SearchBar));

    public static readonly DependencyProperty ShowButtonProperty =
        DependencyProperty.Register(nameof(ShowButton), typeof(bool), typeof(SearchBar),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(SearchBar),
            new PropertyMetadata("Search"));

    public static readonly DependencyProperty SearchWidthProperty =
        DependencyProperty.Register(nameof(SearchWidth), typeof(double), typeof(SearchBar),
            new PropertyMetadata(double.NaN));

    public static readonly DependencyProperty MinSearchWidthProperty =
        DependencyProperty.Register(nameof(MinSearchWidth), typeof(double), typeof(SearchBar),
            new PropertyMetadata(120d));

    public static readonly DependencyProperty TextBoxMarginProperty =
        DependencyProperty.Register(nameof(TextBoxMargin), typeof(Thickness), typeof(SearchBar),
            new PropertyMetadata(new Thickness(0, 0, 6, 0)));

    public SearchBar() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public bool ShowButton
    {
        get => (bool)GetValue(ShowButtonProperty);
        set => SetValue(ShowButtonProperty, value);
    }

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public double SearchWidth
    {
        get => (double)GetValue(SearchWidthProperty);
        set => SetValue(SearchWidthProperty, value);
    }

    public double MinSearchWidth
    {
        get => (double)GetValue(MinSearchWidthProperty);
        set => SetValue(MinSearchWidthProperty, value);
    }

    public Thickness TextBoxMargin
    {
        get => (Thickness)GetValue(TextBoxMarginProperty);
        set => SetValue(TextBoxMarginProperty, value);
    }
}
