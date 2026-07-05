using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NickeltownFinance.Controls;

public partial class ErrorScreen : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ErrorScreen),
            new PropertyMetadata("Something went wrong"));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ErrorScreen),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(ErrorScreen),
            new PropertyMetadata(false, OnIsCompactChanged));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(ErrorScreen));

    public static readonly DependencyProperty RetryTextProperty =
        DependencyProperty.Register(nameof(RetryText), typeof(string), typeof(ErrorScreen),
            new PropertyMetadata("Try again"));

    public static readonly DependencyProperty ContentPaddingProperty =
        DependencyProperty.Register(nameof(ContentPadding), typeof(Thickness), typeof(ErrorScreen),
            new PropertyMetadata(new Thickness(0)));

    public ErrorScreen()
    {
        InitializeComponent();
        UpdatePadding();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public string RetryText
    {
        get => (string)GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    private static void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErrorScreen screen)
            screen.UpdatePadding();
    }

    private void UpdatePadding()
    {
        if (IsCompact && ContentPadding == new Thickness(0))
            ContentPadding = new Thickness(0, 0, 0, 12);
    }
}
