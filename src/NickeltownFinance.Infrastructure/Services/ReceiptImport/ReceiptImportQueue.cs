using System.Diagnostics;
using System.Collections.Concurrent;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

/// <summary>
/// In-process FIFO queue with background pipeline worker.
/// </summary>
public sealed class ReceiptImportQueue : IReceiptImportQueue, IDisposable
{
    private readonly ConcurrentQueue<ObjectId> _pending = new();
    private readonly HashSet<ObjectId> _queuedIds = [];
    private readonly object _queueLock = new();
    private readonly IReceiptImportItemRepository _itemRepository;
    private readonly IDocumentPreviewService _documentPreviewService;
    private readonly IOcrService _ocrService;
    private readonly ISupplierDetectionService _supplierDetection;
    private readonly IReceiptAiParser _aiParser;
    private readonly IReceiptMatchingService _matchingService;
    private readonly IReceiptDuplicateDetector _duplicateDetector;
    private readonly IReceiptProcessingSettingsService _settingsService;
    private readonly IReceiptProcessingLogger _processingLogger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private int _processing;

    public ReceiptImportQueue(
        IReceiptImportItemRepository itemRepository,
        IDocumentPreviewService documentPreviewService,
        IOcrService ocrService,
        ISupplierDetectionService supplierDetection,
        IReceiptAiParser aiParser,
        IReceiptMatchingService matchingService,
        IReceiptDuplicateDetector duplicateDetector,
        IReceiptProcessingSettingsService settingsService,
        IReceiptProcessingLogger processingLogger)
    {
        _itemRepository = itemRepository;
        _documentPreviewService = documentPreviewService;
        _ocrService = ocrService;
        _supplierDetection = supplierDetection;
        _aiParser = aiParser;
        _matchingService = matchingService;
        _duplicateDetector = duplicateDetector;
        _settingsService = settingsService;
        _processingLogger = processingLogger;
        _worker = Task.Run(ProcessLoopAsync);
    }

    public event EventHandler<ReceiptImportItemInfo>? ItemUpdated;

    public int PendingCount => _pending.Count;

    public bool IsProcessing => Volatile.Read(ref _processing) > 0;

    public void Enqueue(ObjectId importItemId)
    {
        lock (_queueLock)
        {
            if (!_queuedIds.Add(importItemId))
                return;

            _pending.Enqueue(importItemId);
        }
    }

    public void NotifyItemUpdated(ReceiptImportItemInfo item) =>
        ItemUpdated?.Invoke(this, item);

    private async Task ProcessLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (!_pending.TryDequeue(out var id))
            {
                await Task.Delay(250, _cts.Token);
                continue;
            }

            lock (_queueLock)
                _queuedIds.Remove(id);

            Interlocked.Increment(ref _processing);
            try
            {
                await ProcessItemAsync(id);
            }
            catch
            {
                // individual item failures handled inside ProcessItemAsync
            }
            finally
            {
                Interlocked.Decrement(ref _processing);
            }
        }
    }

    private async Task ProcessItemAsync(ObjectId id)
    {
        var item = _itemRepository.GetById(id);
        if (item is null) return;

        if (item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Ignored)
            return;

        var settings = _settingsService.GetSettings();
        var stageErrors = new List<string>();
        var stageWarnings = new List<string>();
        var overallSw = Stopwatch.StartNew();

        try
        {
            if (!ReceiptPathHelper.TryResolveFilePath(item.OriginalRelativePath, out var sourcePath, out var pathError))
                throw new InvalidOperationException(pathError ?? "Original receipt file is missing.");

            var isImage = IsImage(item.FileName);
            var isPdf = IsPdf(item.FileName);

            if (isImage || isPdf)
            {
                var stageName = isPdf ? "PDF Rendering" : "Image Enhancement";
                await RunStageAsync(item, ReceiptImportStatus.Preprocessing, stageName, sourcePath, async () =>
                {
                    var previews = await _documentPreviewService.BuildInboxPreviewsAsync(
                        item,
                        sourcePath,
                        settings.AutoImageEnhancement && isImage,
                        stageWarnings);

                    item.PageCount = Math.Max(1, previews.PageCount);
                    if (!string.IsNullOrWhiteSpace(previews.PrimaryPreviewRelativePath))
                        item.ProcessedRelativePath = previews.PrimaryPreviewRelativePath;
                    if (!string.IsNullOrWhiteSpace(previews.OcrImageRelativePath))
                        item.OcrImageRelativePath = previews.OcrImageRelativePath;
                    if (!string.IsNullOrWhiteSpace(previews.ThumbnailRelativePath))
                        item.ThumbnailRelativePath = previews.ThumbnailRelativePath;
                    if (previews.PreviewRelativePaths.Count > 0)
                        item.PreviewPagesJson = DocumentPreviewPaths.Serialize(previews.PreviewRelativePaths.ToList());

                    _itemRepository.Update(item);
                }, stageErrors, stageWarnings);
            }

            if (settings.OcrEnabled)
            {
                await RunStageAsync(item, ReceiptImportStatus.ProcessingOcr, "OCR", sourcePath, async () =>
                {
                    if (!_ocrService.IsAvailable)
                    {
                        stageWarnings.Add("OCR is not available on this device.");
                        return;
                    }

                    var candidates = BuildOcrCandidatePaths(item, sourcePath);
                    var ocr = await _ocrService.ExtractBestAsync(candidates);
                    if (ocr is null)
                    {
                        stageWarnings.Add("OCR could not extract text from the receipt.");
                        return;
                    }

                    ApplyOcr(item, ocr);
                }, stageErrors, stageWarnings);
            }

            if (item.OcrProcessed)
            {
                await RunStageAsync(item, ReceiptImportStatus.SupplierDetection, "Supplier Detection", sourcePath, async () =>
                {
                    var ocr = ReceiptImportService.MapOcr(item);
                    var detection = await _supplierDetection.DetectAsync(item, ocr);
                    if (detection is null) return;

                    item.DetectedSupplierProfileId = detection.ProfileId;
                    item.DetectedSupplierName = detection.SupplierName;
                    item.DetectedSupplierConfidence = detection.Confidence;

                    if (detection.SuggestedCategoryId is not null && detection.SuggestedCategoryId != ObjectId.Empty)
                    {
                        item.AiSuggestedCategoryId = detection.SuggestedCategoryId;
                        item.AiSuggestedCategoryName = detection.SuggestedCategoryName;
                        item.AiCategoryConfidence = detection.Confidence;
                    }
                }, stageErrors, stageWarnings);
            }

            if (settings.AiCategorisation && item.OcrProcessed)
            {
                await RunStageAsync(item, ReceiptImportStatus.AiParsing, "AI Categorisation", sourcePath, async () =>
                {
                    if (!_aiParser.IsAvailable) return;

                    var ocr = ReceiptImportService.MapOcr(item);
                    var suggestion = await _aiParser.ParseAsync(item, ocr);
                    if (suggestion is null) return;

                    item.AiSuggestedCategoryId = suggestion.CategoryId;
                    item.AiSuggestedCategoryName = suggestion.CategoryName;
                    item.AiCategoryConfidence = suggestion.Confidence;
                }, stageErrors, stageWarnings);
            }

            if (settings.BankMatching)
            {
                await RunStageAsync(item, ReceiptImportStatus.MatchingTransaction, "Bank Matching", sourcePath, async () =>
                {
                    var match = await _matchingService.FindBestMatchAsync(item);
                    if (match is null)
                    {
                        item.MatchQuality = ReceiptMatchQuality.None;
                        return;
                    }

                    item.SuggestedMatchTransactionId = match.TransactionId;
                    item.SuggestedMatchConfidence = match.Confidence;
                    item.SuggestedMatchDescription = match.Description;
                    item.SuggestedMatchDate = match.Date;
                    item.SuggestedMatchAmount = match.Amount;
                    item.MatchQuality = match.Quality;
                }, stageErrors, stageWarnings);
            }

            if (settings.DuplicateDetection)
            {
                var duplicate = await _duplicateDetector.CheckAsync(item);
                item.IsPossibleDuplicate = duplicate.IsPossibleDuplicate;
                item.DuplicateWarning = duplicate.Warning;
                if (item.IsPossibleDuplicate && !string.IsNullOrWhiteSpace(item.DuplicateWarning))
                    stageWarnings.Add(item.DuplicateWarning);
            }

            FinalizeStatus(item, stageErrors, stageWarnings);
            _itemRepository.Update(item);
            SafeRaiseUpdated(item, sourcePath);
            _processingLogger.LogStageFinish(
                item.Id,
                "Pipeline",
                (int)overallSw.ElapsedMilliseconds,
                sourcePath: sourcePath);
        }
        catch (Exception ex)
        {
            overallSw.Stop();
            if (item.Status is ReceiptImportStatus.Ready or ReceiptImportStatus.CompletedWithWarnings)
            {
                _processingLogger.LogStageError(
                    item.Id,
                    "PostProcessing",
                    ex,
                    (int)overallSw.ElapsedMilliseconds,
                    sourcePath: item.OriginalRelativePath);
                SafeRaiseUpdated(item, item.OriginalRelativePath);
                return;
            }

            item.Status = ReceiptImportStatus.Failed;
            item.ErrorMessage = ex.Message;
            ReceiptTimelineStore.AddEvent(item, "Failed", detail: ex.Message);
            _itemRepository.Update(item);
            _processingLogger.LogStageError(item.Id, "Pipeline", ex, (int)overallSw.ElapsedMilliseconds, item.OriginalRelativePath);
            SafeRaiseUpdated(item, item.OriginalRelativePath);
        }
    }

    private static void FinalizeStatus(
        ReceiptImportItem item,
        List<string> stageErrors,
        List<string> stageWarnings)
    {
        item.HasProcessingWarnings = stageWarnings.Count > 0 || item.IsPossibleDuplicate;

        if (stageErrors.Count > 0)
        {
            item.Status = ReceiptImportStatus.Failed;
            item.ErrorMessage = string.Join("; ", stageErrors);
            ReceiptTimelineStore.AddEvent(item, "Failed", detail: item.ErrorMessage);
            return;
        }

        if (stageWarnings.Count > 0)
        {
            item.Status = ReceiptImportStatus.CompletedWithWarnings;
            item.ErrorMessage = string.Join("; ", stageWarnings.Distinct());
            ReceiptTimelineStore.AddEvent(item, "CompletedWithWarnings", detail: item.ErrorMessage);
            return;
        }

        item.Status = ReceiptImportStatus.Ready;
        item.ErrorMessage = null;
        ReceiptTimelineStore.AddEvent(item, "Completed");
    }

    private async Task RunStageAsync(
        ReceiptImportItem item,
        ReceiptImportStatus status,
        string stageName,
        string sourcePath,
        Func<Task> work,
        List<string> stageErrors,
        List<string> stageWarnings,
        bool treatExceptionAsWarning = true)
    {
        var sw = Stopwatch.StartNew();
        item.Status = status;
        _processingLogger.LogStageStart(item.Id, stageName, sourcePath);
        _itemRepository.Update(item);
        SafeRaiseUpdated(item, sourcePath);

        try
        {
            await work();
            sw.Stop();
            ReceiptTimelineStore.AddEvent(item, stageName, durationMs: (int)sw.ElapsedMilliseconds);
            _processingLogger.LogStageFinish(item.Id, stageName, (int)sw.ElapsedMilliseconds, sourcePath: sourcePath);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _processingLogger.LogStageError(item.Id, stageName, ex, (int)sw.ElapsedMilliseconds, sourcePath);
            var message = $"{stageName}: {ex.Message}";
            if (treatExceptionAsWarning)
                stageWarnings.Add(message);
            else
                stageErrors.Add(message);
        }
    }

    private static void ApplyOcr(ReceiptImportItem item, OcrExtractionResult ocr)
    {
        item.OcrProcessed = true;
        item.OcrSupplier = ocr.Supplier;
        item.OcrSupplierConfidence = ocr.SupplierConfidence;
        item.OcrDate = ocr.Date;
        item.OcrDateConfidence = ocr.DateConfidence;
        item.OcrInvoiceNumber = ocr.InvoiceNumber;
        item.OcrInvoiceNumberConfidence = ocr.InvoiceNumberConfidence;
        item.OcrAbn = ocr.Abn;
        item.OcrAbnConfidence = ocr.AbnConfidence;
        item.OcrSubtotal = ocr.Subtotal;
        item.OcrSubtotalConfidence = ocr.SubtotalConfidence;
        item.OcrGst = ocr.Gst;
        item.OcrGstConfidence = ocr.GstConfidence;
        item.OcrTotal = ocr.Total;
        item.OcrTotalConfidence = ocr.TotalConfidence;
        item.OcrPaymentMethod = ocr.PaymentMethod;
        item.OcrPaymentMethodConfidence = ocr.PaymentMethodConfidence;
        item.OcrFullText = ocr.FullText;
        item.OcrCurrency = ocr.Currency;
        item.OcrCurrencyConfidence = ocr.CurrencyConfidence;
    }

    private void RaiseUpdated(ReceiptImportItem item) =>
        ItemUpdated?.Invoke(this, ReceiptImportService.Map(item));

    private void SafeRaiseUpdated(ReceiptImportItem item, string? sourcePath)
    {
        try
        {
            RaiseUpdated(item);
        }
        catch (Exception ex)
        {
            _processingLogger.LogStageError(item.Id, "ItemUpdated", ex, sourcePath: sourcePath);
        }
    }

    private static List<string> BuildOcrCandidatePaths(ReceiptImportItem item, string originalPath)
    {
        var paths = new List<string>();

        if (ReceiptPathHelper.TryResolveFilePath(item.OriginalRelativePath, out var original, out _))
            paths.Add(original);

        if (!string.IsNullOrWhiteSpace(item.ProcessedRelativePath)
            && ReceiptPathHelper.TryResolveFilePath(item.ProcessedRelativePath, out var preview, out _))
            paths.Add(preview);

        if (!string.IsNullOrWhiteSpace(item.OcrImageRelativePath)
            && ReceiptPathHelper.TryResolveFilePath(item.OcrImageRelativePath, out var ocrImage, out _))
            paths.Add(ocrImage);

        var pdfPages = DocumentPreviewPaths.Parse(item.PreviewPagesJson)
            .Select(AppPaths.ResolvePath)
            .Where(File.Exists);
        paths.AddRange(pdfPages);

        if (paths.Count == 0 && File.Exists(originalPath))
            paths.Add(originalPath);

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPdf(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        _cts.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(2)); } catch { /* shutting down */ }
        _cts.Dispose();
    }
}
