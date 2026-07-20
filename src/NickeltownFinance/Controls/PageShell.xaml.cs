using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace NickeltownFinance.Controls;

/// <summary>
/// Page host that layers content with shared loading and error screens.
/// </summary>
[ContentProperty(nameof(Body))]
public partial class PageShell : UserControl
{
    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(PageShell));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(PageShell),
            new PropertyMetadata(false));

    public static readonly DependencyProperty LoadingMessageProperty =
        DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(PageShell),
            new PropertyMetadata("On track…"));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(PageShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorTitleProperty =
        DependencyProperty.Register(nameof(ErrorTitle), typeof(string), typeof(PageShell),
            new PropertyMetadata("Something went wrong"));

    public static readonly DependencyProperty IsErrorCompactProperty =
        DependencyProperty.Register(nameof(IsErrorCompact), typeof(bool), typeof(PageShell),
            new PropertyMetadata(true));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(PageShell));

    public static readonly DependencyProperty ErrorVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(ErrorVerticalAlignment), typeof(VerticalAlignment), typeof(PageShell),
            new PropertyMetadata(VerticalAlignment.Top));

    public static readonly DependencyProperty ErrorHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(ErrorHorizontalAlignment), typeof(HorizontalAlignment), typeof(PageShell),
            new PropertyMetadata(HorizontalAlignment.Stretch));

    public PageShell() => InitializeComponent();

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public string LoadingMessage
    {
        get => (string)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    public string? ErrorMessage
    {
        get => (string?)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string ErrorTitle
    {
        get => (string)GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    public bool IsErrorCompact
    {
        get => (bool)GetValue(IsErrorCompactProperty);
        set => SetValue(IsErrorCompactProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public VerticalAlignment ErrorVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(ErrorVerticalAlignmentProperty);
        set => SetValue(ErrorVerticalAlignmentProperty, value);
    }

    public HorizontalAlignment ErrorHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(ErrorHorizontalAlignmentProperty);
        set => SetValue(ErrorHorizontalAlignmentProperty, value);
    }
}
