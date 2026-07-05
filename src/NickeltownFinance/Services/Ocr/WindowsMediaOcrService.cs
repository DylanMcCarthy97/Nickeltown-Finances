using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Helpers;

namespace NickeltownFinance.Services.Ocr;

/// <summary>
/// Windows.Media.Ocr implementation — swappable via <see cref="IOcrService"/>.
/// WinRT OCR must run on an STA thread.
/// </summary>
public sealed class WindowsMediaOcrService : IOcrService
{
    private readonly IReceiptOcrFieldParser _fieldParser;
    private readonly ILogger<WindowsMediaOcrService> _logger;
    private bool? _isAvailable;

    public WindowsMediaOcrService(
        IReceiptOcrFieldParser fieldParser,
        ILogger<WindowsMediaOcrService> logger)
    {
        _fieldParser = fieldParser;
        _logger = logger;
    }

    public bool IsAvailable
    {
        get
        {
            if (_isAvailable.HasValue)
                return _isAvailable.Value;

            try
            {
                _isAvailable = StaTaskRunner.RunAsync(() =>
                    OcrEngine.TryCreateFromUserProfileLanguages() is not null).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR availability check failed.");
                _isAvailable = false;
            }

            return _isAvailable.Value;
        }
    }

    public async Task<OcrExtractionResult?> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("OCR skipped; file not found: {FilePath}", filePath);
            return null;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".pdf")
            return null;

        var (ocrPath, isTemp) = await ReceiptPathHelper.PrepareOcrPathAsync(filePath, cancellationToken);
        try
        {
            return await StaTaskRunner.RunAsync(
                ct => ExtractOnStaThreadAsync(ocrPath, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR failed for {FilePath}", filePath);
            return null;
        }
        finally
        {
            ReceiptPathHelper.TryDeleteTempFile(ocrPath, isTemp);
        }
    }

    public async Task<OcrExtractionResult?> ExtractBestAsync(
        IReadOnlyList<string> candidatePaths,
        CancellationToken cancellationToken = default)
    {
        OcrExtractionResult? best = null;
        var bestScore = -1;
        string? bestPath = null;

        foreach (var path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var result = await ExtractAsync(path, cancellationToken);
            if (result is null)
                continue;

            var score = ScoreResult(result);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = result;
            bestPath = path;
        }

        if (best is not null && bestPath is not null)
            _logger.LogInformation("OCR selected {FilePath} with score {Score}", bestPath, bestScore);

        return best;
    }

    private async Task<OcrExtractionResult?> ExtractOnStaThreadAsync(string ocrPath, CancellationToken cancellationToken)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            _logger.LogWarning("OCR engine unavailable on this device.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var file = await StorageFile.GetFileFromPathAsync(ocrPath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        var ocrResult = await engine.RecognizeAsync(softwareBitmap);
        var fullText = ocrResult?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullText))
        {
            _logger.LogInformation("OCR returned no text for {FilePath}", ocrPath);
            return null;
        }

        return _fieldParser.Parse(fullText);
    }

    internal static int ScoreResult(OcrExtractionResult result)
    {
        var confidences = new List<byte>();
        if (result.SupplierConfidence is { } s) confidences.Add(s);
        if (result.DateConfidence is { } d) confidences.Add(d);
        if (result.TotalConfidence is { } t) confidences.Add(t);
        if (result.InvoiceNumberConfidence is { } i) confidences.Add(i);
        if (result.AbnConfidence is { } a) confidences.Add(a);

        var confidenceScore = confidences.Count == 0 ? 0 : confidences.Average(x => x);
        var textScore = Math.Min(40, (result.FullText?.Length ?? 0) / 25);
        var fieldScore = 0;
        if (!string.IsNullOrWhiteSpace(result.Supplier)) fieldScore += 10;
        if (result.Total is not null) fieldScore += 15;
        if (result.Date is not null) fieldScore += 10;
        if (!string.IsNullOrWhiteSpace(result.InvoiceNumber)) fieldScore += 5;

        return (int)confidenceScore + textScore + fieldScore;
    }
}
