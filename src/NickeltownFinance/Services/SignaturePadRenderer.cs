using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NickeltownFinance.Services;

/// <summary>Renders an <see cref="InkCanvas"/> signature to a PNG file.</summary>
public static class SignaturePadRenderer
{
    public static void SaveToPng(InkCanvas pad, string outputPath, int maxWidth = 480, int maxHeight = 160)
    {
        ArgumentNullException.ThrowIfNull(pad);
        if (pad.Strokes.Count == 0)
            throw new InvalidOperationException("Draw your signature before saving.");

        var bounds = pad.Strokes.GetBounds();
        const double padding = 16;
        var contentWidth = Math.Max(bounds.Width + padding * 2, 1);
        var contentHeight = Math.Max(bounds.Height + padding * 2, 1);

        var scale = Math.Min(maxWidth / contentWidth, maxHeight / contentHeight);
        scale = Math.Clamp(scale, 0.5, 2.5);

        var renderWidth = (int)Math.Ceiling(contentWidth * scale);
        var renderHeight = (int)Math.Ceiling(contentHeight * scale);
        renderWidth = Math.Clamp(renderWidth, 120, maxWidth);
        renderHeight = Math.Clamp(renderHeight, 48, maxHeight);

        var strokes = pad.Strokes.Clone();
        var transform = new Matrix();
        transform.Translate(-bounds.X + padding, -bounds.Y + padding);
        transform.Scale(scale, scale);
        strokes.Transform(transform, false);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Transparent background — reports print on white paper.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, renderWidth, renderHeight));
            strokes.Draw(dc);
        }

        var bitmap = new RenderTargetBitmap(renderWidth, renderHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
