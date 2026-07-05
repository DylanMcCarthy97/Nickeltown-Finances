using System.Windows;
using System.Windows.Media;
using NickeltownFinance.Services;

namespace NickeltownFinance.Views.Dialogs;

public partial class SignatureCaptureWindow
{
    public string? SavedPath { get; private set; }

    public SignatureCaptureWindow()
    {
        InitializeComponent();
        SignaturePad.DefaultDrawingAttributes.Color = Colors.Black;
        SignaturePad.DefaultDrawingAttributes.Width = 2.4;
        SignaturePad.DefaultDrawingAttributes.Height = 2.4;
        SignaturePad.DefaultDrawingAttributes.FitToCurve = true;
    }

    /// <summary>Shows the signature pad and returns the temp PNG path, or null if cancelled.</summary>
    public static string? Capture(Window? owner)
    {
        var window = new SignatureCaptureWindow();
        if (owner is not null)
            window.Owner = owner;

        return window.ShowDialog() == true ? window.SavedPath : null;
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        SignaturePad.Strokes.Clear();
        HideError();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        HideError();

        if (SignaturePad.Strokes.Count == 0)
        {
            ShowError("Draw your signature in the box before saving.");
            return;
        }

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"nickeltown-signature-{Guid.NewGuid():N}.png");
            SignaturePadRenderer.SaveToPng(SignaturePad, path);
            SavedPath = path;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
