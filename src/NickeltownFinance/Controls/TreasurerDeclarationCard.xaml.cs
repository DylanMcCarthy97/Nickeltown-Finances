using System.Windows;
using System.Windows.Controls;

namespace NickeltownFinance.Controls;

public partial class TreasurerDeclarationCard : UserControl
{
    public static readonly DependencyProperty DeclarationTextProperty =
        DependencyProperty.Register(nameof(DeclarationText), typeof(string), typeof(TreasurerDeclarationCard),
            new PropertyMetadata(string.Empty));

    public TreasurerDeclarationCard() => InitializeComponent();

    public string DeclarationText
    {
        get => (string)GetValue(DeclarationTextProperty);
        set => SetValue(DeclarationTextProperty, value);
    }
}
