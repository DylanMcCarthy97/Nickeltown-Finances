using Docnet.Core;
using Docnet.Core.Models;
using OpenCvSharp;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.Document;

public sealed class PdfRenderService : IPdfRenderService
{
    public const int DefaultRenderWidth = 1800;
    public const int MaxPagesPerDocument = 100;

    public bool IsAvailable { get; } = true;

    public int GetPageCount(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            return 0;

        try
        {
            var bytes = File.ReadAllBytes(pdfPath);
            using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(DefaultRenderWidth, DefaultRenderWidth * 2));
            return reader.GetPageCount();
        }
        catch
        {
            return 0;
        }
    }

    public Task<string?> RenderPageAsync(
        string pdfPath,
        string outputFilePath,
        int pageIndex,
        int renderWidth = DefaultRenderWidth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(pdfPath))
            return Task.FromResult<string?>(null);

        try
        {
            var bytes = File.ReadAllBytes(pdfPath);
            using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(renderWidth, renderWidth * 2));
            if (pageIndex < 0 || pageIndex >= reader.GetPageCount())
                return Task.FromResult<string?>(null);

            using var pageReader = reader.GetPageReader(pageIndex);
            if (!TrySavePageAsJpeg(pageReader.GetPageWidth(), pageReader.GetPageHeight(), pageReader.GetImage(), outputFilePath))
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>(outputFilePath);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public async Task<IReadOnlyList<string>> RenderAllPagesAsync(
        string pdfPath,
        string outputDirectory,
        int renderWidth = DefaultRenderWidth,
        int? maxPages = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
            return [];

        Directory.CreateDirectory(outputDirectory);
        var limit = maxPages ?? MaxPagesPerDocument;

        try
        {
            var bytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
            using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(renderWidth, renderWidth * 2));
            var pageCount = Math.Min(reader.GetPageCount(), limit);
            var outputs = new List<string>(pageCount);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outputPath = Path.Combine(outputDirectory, $"page{pageIndex + 1}.jpg");
                using var pageReader = reader.GetPageReader(pageIndex);
                if (TrySavePageAsJpeg(pageReader.GetPageWidth(), pageReader.GetPageHeight(), pageReader.GetImage(), outputPath))
                    outputs.Add(outputPath);
            }

            return outputs;
        }
        catch
        {
            return [];
        }
    }

    private static bool TrySavePageAsJpeg(int width, int height, byte[] rawBytes, string outputFilePath)
    {
        if (width <= 0 || height <= 0)
            return false;

        if (rawBytes.Length == 0)
            return false;

        using var rgba = Mat.FromPixelData(height, width, MatType.CV_8UC4, rawBytes);
        using var bgr = new Mat();
        Cv2.CvtColor(rgba, bgr, ColorConversionCodes.BGRA2BGR);

        Cv2.ImEncode(".jpg", bgr, out var jpegBytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 88));
        if (jpegBytes.Length == 0)
            return false;

        ReceiptPathHelper.WriteBytes(outputFilePath, jpegBytes);
        return true;
    }
}
