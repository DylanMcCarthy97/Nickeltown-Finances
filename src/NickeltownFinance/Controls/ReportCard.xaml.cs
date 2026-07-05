using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace NickeltownFinance.Controls;

[ContentProperty(nameof(Body))]
public partial class ReportCard : UserControl
{
    public static readonly DependencyProperty ClubNameProperty =
        DependencyProperty.Register(nameof(ClubName), typeof(string), typeof(ReportCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ReportTitleProperty =
        DependencyProperty.Register(nameof(ReportTitle), typeof(string), typeof(ReportCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ReportCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(ReportCard));

    public ReportCard() => InitializeComponent();

    public string ClubName
    {
        get => (string)GetValue(ClubNameProperty);
        set => SetValue(ClubNameProperty, value);
    }

    public string ReportTitle
    {
        get => (string)GetValue(ReportTitleProperty);
        set => SetValue(ReportTitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }
}
