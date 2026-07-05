using System.Windows;
using System.Windows.Controls;
using NickeltownFinance.Core.Formatting;

namespace NickeltownFinance.Controls;

public partial class AppDateField : UserControl
{
    public const string DisplayFormat = DateFormats.Date;

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(AppDateField),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(AppDateField),
            new PropertyMetadata(DisplayFormat));

    public AppDateField()
    {
        InitializeComponent();
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }
}
