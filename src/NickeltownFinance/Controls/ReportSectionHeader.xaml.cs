using System.Windows;
using System.Windows.Controls;

namespace NickeltownFinance.Controls;

public partial class ReportSectionHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ReportSectionHeader),
            new PropertyMetadata(string.Empty));

    public ReportSectionHeader() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
