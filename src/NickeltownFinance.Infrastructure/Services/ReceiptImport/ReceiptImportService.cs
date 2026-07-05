using System.Collections.Concurrent;
using System.Security.Cryptography;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptImportService : IReceiptImportService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    private readonly IReceiptImportItemRepository _itemRepository;
    private readonly IReceiptImportQueue _queue;
    private readonly IAttachmentService _attachmentService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISessionService _sessionService;
    private readonly IFinancialYearService _financialYearService;
    private readonly ISupplierDetectionService _supplierDetection;
    private readonly ConcurrentDictionary<ObjectId, SemaphoreSlim> _commitGates = new();

    public ReceiptImportService(
        IReceiptImportItemRepository itemRepository,
        IReceiptImportQueue queue,
        IAttachmentService attachmentService,
        ITransactionRepository transactionRepository,
        ISessionService sessionService,
        IFinancialYearService financialYearService,
        ISupplierDetectionService supplierDetection)
    {
        _itemRepository = itemRepository;
        _queue = queue;
        _attachmentService = attachmentService;
        _transactionRepository = transactionRepository;
        _sessionService = sessionService;
        _financialYearService = financialYearService;
        _supplierDetection = supplierDetection;
        _queue.ItemUpdated += OnQueueItemUpdated;
    }

    private async void OnQueueItemUpdated(object? sender, ReceiptImportItemInfo item)
    {
        if (item.ImportTarget != ReceiptImportTarget.Transaction)
            return;

        if (item.Status is not (
            ReceiptImportStatus.Ready
            or ReceiptImportStatus.CompletedWithWarnings
            or ReceiptImportStatus.Failed))
            return;

        if (item.PendingTransactionId is not { } txnId || txnId == ObjectId.Empty)
            return;

        try
        {
            await TryAutoAttachAsync(item.Id);
        }
        catch
        {
            // auto-attach is best-effort; user can retry from editor
        }
    }

    public Task<IReadOnlyList<ReceiptImportItemInfo>> GetInboxAsync(bool includeCommitted = false)
    {
        var items = _itemRepository.GetInbox(includeCommitted)
            .Select(Map)
            .ToList();
        return Task.FromResult<IReadOnlyList<ReceiptImportItemInfo>>(items);
    }

    public Task<ReceiptImportItemInfo?> GetByIdAsync(ObjectId id)
    {
        var item = _itemRepository.GetById(id);
        return Task.FromResult(item is null ? null : Map(item));
    }

    public async Task<IReadOnlyList<ReceiptImportItemInfo>> ImportFromDesktopAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ReceiptImportItemInfo>();
        foreach (var path in ExpandPaths(paths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;

            var ext = Path.GetExtension(path);
            if (!Supported.Contains(ext)) continue;

            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            var info = await EnqueueFromUploadAsync(
                data,
                Path.GetFileName(path),
                GuessContentType(ext),
                ReceiptImportSource.Desktop,
                cancellationToken: cancellationToken);
            results.Add(info);
        }

        return results;
    }

    public async Task<ReceiptImportItemInfo> EnqueueFromUploadAsync(
        byte[] data,
        string fileName,
        string contentType,
        ReceiptImportSource source,
        string? deviceName = null,
        string? userAgent = null,
        ObjectId? pendingTransactionId = null,
        string? uploadSessionKey = null,
        ReceiptImportTarget importTarget = ReceiptImportTarget.Inbox,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!Supported.Contains(ext))
            throw new InvalidOperationException($"Unsupported file type: {ext}");

        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var existing = _itemRepository.FindByFileHash(hash);
        if (existing is not null
            && importTarget == ReceiptImportTarget.Inbox
            && existing.ImportTarget == ReceiptImportTarget.Inbox
            && existing.Status is not ReceiptImportStatus.Committed and not ReceiptImportStatus.Ignored)
            return Map(existing);

        var item = new ReceiptImportItem
        {
            Status = ReceiptImportStatus.Queued,
            Source = source,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = data.LongLength,
            FileHash = hash,
            UploadDeviceName = deviceName,
            UploadDeviceUserAgent = userAgent,
            PendingTransactionId = pendingTransactionId,
            UploadSessionKey = uploadSessionKey,
            ImportTarget = importTarget
        };

        _itemRepository.Insert(item);

        var dir = Path.Combine(AppPaths.InboxPath, item.Id.ToString());
        Directory.CreateDirectory(dir);
        var storedName = $"original{ext}";
        var fullPath = Path.Combine(dir, storedName);
        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);

        item.OriginalRelativePath = AppPaths.ToRelativePath(fullPath);
        ReceiptTimelineStore.AddEvent(item, "Uploaded", detail: source.ToString());
        _itemRepository.Update(item);

        _queue.Enqueue(item.Id);
        var info = Map(item);
        try { _queue.NotifyItemUpdated(info); }
        catch { /* upload already persisted; notification must not fail the caller */ }
        return info;
    }

    public async Task CommitAsync(ReceiptImportCommitRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _commitGates.GetOrAdd(request.ImportItemId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await CommitCoreAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task CommitCoreAsync(ReceiptImportCommitRequest request, CancellationToken cancellationToken)
    {
        var item = _itemRepository.GetById(request.ImportItemId)
            ?? throw new InvalidOperationException("Import item not found.");

        if (item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Ignored)
        {
            // Idempotent — auto-attach and save can both request the same commit.
            if (request.Action is ReceiptMatchAction.Match or ReceiptMatchAction.CreateNew)
                return;

            throw new InvalidOperationException("Import item already resolved.");
        }

        switch (request.Action)
        {
            case ReceiptMatchAction.Ignore:
                item.Status = ReceiptImportStatus.Ignored;
                _itemRepository.Update(item);
                _queue.NotifyItemUpdated(Map(item));
                return;

            case ReceiptMatchAction.Match:
            {
                var txnId = request.TransactionId ?? item.SuggestedMatchTransactionId
                    ?? throw new InvalidOperationException("No transaction selected for match.");
                var filePath = ResolveProcessingPath(item);
                var attachment = await _attachmentService.AddFromInboxAsync(
                    txnId, filePath, item.FileName, request.AttachmentKind, item);
                item.MatchedTransactionId = txnId;
                item.ResultAttachmentId = attachment.Id;
                item.Status = ReceiptImportStatus.Committed;
                ReceiptTimelineStore.AddEvent(item, "Committed", userName: _sessionService.CurrentUser?.DisplayName);
                _itemRepository.Update(item);
                await RecordSupplierPurchaseAsync(item);
                _queue.NotifyItemUpdated(Map(item));
                return;
            }

            case ReceiptMatchAction.CreateNew:
            {
                var txn = BuildTransactionFromItem(item, request);
                _transactionRepository.Insert(txn);
                var filePath = ResolveProcessingPath(item);
                var attachment = await _attachmentService.AddFromInboxAsync(
                    txn.Id, filePath, item.FileName, request.AttachmentKind, item);
                item.MatchedTransactionId = txn.Id;
                item.ResultAttachmentId = attachment.Id;
                item.Status = ReceiptImportStatus.Committed;
                ReceiptTimelineStore.AddEvent(item, "Committed", userName: _sessionService.CurrentUser?.DisplayName);
                _itemRepository.Update(item);
                await RecordSupplierPurchaseAsync(item);
                _queue.NotifyItemUpdated(Map(item));
                return;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(request.Action));
        }
    }

    public Task IgnoreAsync(ObjectId importItemId) =>
        CommitAsync(new ReceiptImportCommitRequest
        {
            ImportItemId = importItemId,
            Action = ReceiptMatchAction.Ignore
        });

    public async Task TryAutoAttachAsync(ObjectId importItemId, CancellationToken cancellationToken = default)
    {
        var gate = _commitGates.GetOrAdd(importItemId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var item = _itemRepository.GetById(importItemId);
            if (item is null) return;

            if (item.ImportTarget != ReceiptImportTarget.Transaction)
                return;

            if (item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Ignored)
                return;

            if (item.Status is not (
                ReceiptImportStatus.Ready
                or ReceiptImportStatus.CompletedWithWarnings
                or ReceiptImportStatus.Failed))
                return;

            if (item.PendingTransactionId is not { } txnId || txnId == ObjectId.Empty)
                return;

            if (item.ResultAttachmentId is not null && item.ResultAttachmentId != ObjectId.Empty)
                return;

            if (_transactionRepository.GetById(txnId) is null)
                return;

            var hadProcessingIssues = item.Status is ReceiptImportStatus.Failed or ReceiptImportStatus.CompletedWithWarnings
                || item.HasProcessingWarnings
                || !string.IsNullOrWhiteSpace(item.ErrorMessage);

            await CommitCoreAsync(new ReceiptImportCommitRequest
            {
                ImportItemId = importItemId,
                Action = ReceiptMatchAction.Match,
                TransactionId = txnId,
                AttachmentKind = AttachmentKind.Receipt
            }, cancellationToken);

            if (!hadProcessingIssues)
                return;

            var updated = _itemRepository.GetById(importItemId);
            if (updated is null) return;

            updated.AttachedDespiteProcessingFailure = true;
            _itemRepository.Update(updated);
            _queue.NotifyItemUpdated(Map(updated));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AttachSessionItemsToTransactionAsync(
        string uploadSessionKey,
        ObjectId transactionId,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _itemRepository.GetByUploadSessionKey(uploadSessionKey))
        {
            if (item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Ignored)
                continue;

            if (item.ResultAttachmentId is not null && item.ResultAttachmentId != ObjectId.Empty)
                continue;

            item.PendingTransactionId = transactionId;
            _itemRepository.Update(item);
            _queue.NotifyItemUpdated(Map(item));

            if (item.Status is ReceiptImportStatus.Ready
                or ReceiptImportStatus.CompletedWithWarnings
                or ReceiptImportStatus.Failed)
            {
                await TryAutoAttachAsync(item.Id, cancellationToken);
            }
        }
    }

    public async Task ResolveSessionUploadsOnCancelAsync(
        string uploadSessionKey,
        bool keepInInbox,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in _itemRepository.GetByUploadSessionKey(uploadSessionKey))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (keepInInbox)
            {
                item.ImportTarget = ReceiptImportTarget.Inbox;
                item.PendingTransactionId = null;
                item.UploadSessionKey = null;
                _itemRepository.Update(item);
                _queue.NotifyItemUpdated(Map(item));
            }
            else
            {
                await DeleteAsync(item.Id);
            }
        }
    }

    public Task RetryAsync(ObjectId importItemId)
    {
        var item = _itemRepository.GetById(importItemId)
            ?? throw new InvalidOperationException("Import item not found.");

        if (item.Status is ReceiptImportStatus.Committed or ReceiptImportStatus.Ignored)
            throw new InvalidOperationException("Cannot retry a resolved import item.");

        item.Status = ReceiptImportStatus.Queued;
        item.ErrorMessage = null;
        item.HasProcessingWarnings = false;
        item.RetryCount++;
        ReceiptTimelineStore.ResetTimeline(item);
        ReceiptTimelineStore.AddEvent(item, "Retry", detail: $"Attempt {item.RetryCount}");
        _itemRepository.Update(item);
        _queue.Enqueue(item.Id);
        _queue.NotifyItemUpdated(Map(item));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ObjectId importItemId)
    {
        var item = _itemRepository.GetById(importItemId);
        if (item is null) return Task.CompletedTask;

        TryDeleteDirectory(Path.Combine(AppPaths.InboxPath, importItemId.ToString()));
        _itemRepository.Delete(importItemId);
        return Task.CompletedTask;
    }

    public Task SaveOcrCorrectionsAsync(ReceiptOcrCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var item = _itemRepository.GetById(request.ImportItemId)
            ?? throw new InvalidOperationException("Import item not found.");

        item.CorrectedSupplier = request.Supplier;
        item.CorrectedDate = request.Date;
        item.CorrectedInvoiceNumber = request.InvoiceNumber;
        item.CorrectedAbn = request.Abn;
        item.CorrectedSubtotal = request.Subtotal;
        item.CorrectedGst = request.Gst;
        item.CorrectedTotal = request.Total;
        item.CorrectedPaymentMethod = request.PaymentMethod;

        ReceiptTimelineStore.AddEvent(item, "OCR Corrected", userName: _sessionService.CurrentUser?.DisplayName);
        _itemRepository.Update(item);
        _queue.NotifyItemUpdated(Map(item));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReceiptImportItemInfo>> SearchInboxAsync(string query, CancellationToken cancellationToken = default)
    {
        var items = _itemRepository.Search(query, includeCommitted: true).Select(Map).ToList();
        return Task.FromResult<IReadOnlyList<ReceiptImportItemInfo>>(items);
    }

    private async Task RecordSupplierPurchaseAsync(ReceiptImportItem item)
    {
        if (item.DetectedSupplierProfileId is not { } profileId || profileId == ObjectId.Empty)
            return;

        var amount = item.CorrectedTotal ?? item.OcrTotal ?? 0;
        var date = item.CorrectedDate ?? item.OcrDate ?? DateTime.Today;
        var categoryId = item.AiSuggestedCategoryId;
        await _supplierDetection.RecordPurchaseAsync(profileId, amount, date, categoryId);
    }

    internal static ReceiptImportItemInfo Map(ReceiptImportItem item) => new()
    {
        Id = item.Id,
        Status = item.Status,
        Source = item.Source,
        FileName = item.FileName,
        OriginalFullPath = AppPaths.ResolvePath(item.OriginalRelativePath),
        ProcessedFullPath = string.IsNullOrWhiteSpace(item.ProcessedRelativePath)
            ? null
            : AppPaths.ResolvePath(item.ProcessedRelativePath),
        ThumbnailFullPath = string.IsNullOrWhiteSpace(item.ThumbnailRelativePath)
            ? null
            : AppPaths.ResolvePath(item.ThumbnailRelativePath),
        PageCount = item.PageCount > 0 ? item.PageCount : 1,
        PreviewFullPaths = ResolvePreviewFullPaths(item),
        OcrImageFullPath = string.IsNullOrWhiteSpace(item.OcrImageRelativePath)
            ? null
            : AppPaths.ResolvePath(item.OcrImageRelativePath),
        ContentType = item.ContentType,
        SizeBytes = item.SizeBytes,
        CreatedDate = item.CreatedDate,
        UploadDeviceName = item.UploadDeviceName,
        ErrorMessage = item.ErrorMessage,
        Ocr = item.OcrProcessed ? MapOcr(item) : null,
        AiSuggestion = (item.AiSuggestedCategoryId is not null && item.AiSuggestedCategoryId != ObjectId.Empty)
                       || !string.IsNullOrWhiteSpace(item.AiSuggestedCategoryName)
            ? new ReceiptAiSuggestionInfo
            {
                CategoryId = item.AiSuggestedCategoryId,
                CategoryName = item.AiSuggestedCategoryName,
                Confidence = item.AiCategoryConfidence
            }
            : null,
        MatchSuggestion = item.SuggestedMatchTransactionId is { } matchId && matchId != ObjectId.Empty
            ? new ReceiptMatchSuggestionInfo
            {
                TransactionId = matchId,
                Description = item.SuggestedMatchDescription ?? string.Empty,
                Date = item.SuggestedMatchDate ?? default,
                Amount = item.SuggestedMatchAmount ?? 0,
                Confidence = item.SuggestedMatchConfidence ?? 0,
                Quality = item.MatchQuality
            }
            : null,
        MatchQuality = item.MatchQuality,
        IsPossibleDuplicate = item.IsPossibleDuplicate,
        DuplicateWarning = item.DuplicateWarning,
        Timeline = ReceiptTimelineStore.Parse(item.TimelineJson),
        DetectedSupplierName = item.DetectedSupplierName,
        DetectedSupplierConfidence = item.DetectedSupplierConfidence,
        PendingTransactionId = item.PendingTransactionId,
        UploadSessionKey = item.UploadSessionKey,
        ImportTarget = item.ImportTarget,
        HasProcessingWarnings = item.HasProcessingWarnings,
        AttachedDespiteProcessingFailure = item.AttachedDespiteProcessingFailure
    };

    internal static ReceiptOcrInfo MapOcr(ReceiptImportItem item) => new()
    {
        Supplier = item.OcrSupplier,
        SupplierConfidence = item.OcrSupplierConfidence,
        CorrectedSupplier = item.CorrectedSupplier,
        Date = item.OcrDate,
        DateConfidence = item.OcrDateConfidence,
        CorrectedDate = item.CorrectedDate,
        InvoiceNumber = item.OcrInvoiceNumber,
        InvoiceNumberConfidence = item.OcrInvoiceNumberConfidence,
        CorrectedInvoiceNumber = item.CorrectedInvoiceNumber,
        Abn = item.OcrAbn,
        AbnConfidence = item.OcrAbnConfidence,
        CorrectedAbn = item.CorrectedAbn,
        Subtotal = item.OcrSubtotal,
        SubtotalConfidence = item.OcrSubtotalConfidence,
        CorrectedSubtotal = item.CorrectedSubtotal,
        Gst = item.OcrGst,
        GstConfidence = item.OcrGstConfidence,
        CorrectedGst = item.CorrectedGst,
        Total = item.OcrTotal,
        TotalConfidence = item.OcrTotalConfidence,
        CorrectedTotal = item.CorrectedTotal,
        PaymentMethod = item.OcrPaymentMethod,
        PaymentMethodConfidence = item.OcrPaymentMethodConfidence,
        CorrectedPaymentMethod = item.CorrectedPaymentMethod,
        FullText = item.OcrFullText,
        Currency = item.OcrCurrency,
        CurrencyConfidence = item.OcrCurrencyConfidence
    };

    private static string ResolveProcessingPath(ReceiptImportItem item)
    {
        var previews = ResolvePreviewFullPaths(item);
        if (previews.Count > 0)
            return previews[0];

        return string.IsNullOrWhiteSpace(item.ProcessedRelativePath)
            ? AppPaths.ResolvePath(item.OriginalRelativePath)
            : AppPaths.ResolvePath(item.ProcessedRelativePath);
    }

    private static IReadOnlyList<string> ResolvePreviewFullPaths(ReceiptImportItem item)
    {
        var fromJson = DocumentPreviewPaths.Parse(item.PreviewPagesJson)
            .Select(AppPaths.ResolvePath)
            .Where(File.Exists)
            .ToList();

        if (fromJson.Count > 0)
            return fromJson;

        if (!string.IsNullOrWhiteSpace(item.ProcessedRelativePath))
        {
            var processed = AppPaths.ResolvePath(item.ProcessedRelativePath);
            if (File.Exists(processed))
                return [processed];
        }

        return [];
    }

    private Transaction BuildTransactionFromItem(ReceiptImportItem item, ReceiptImportCommitRequest request)
    {
        var user = _sessionService.CurrentUser;
        var amount = request.Amount ?? item.CorrectedTotal ?? item.OcrTotal ?? 0;
        var date = request.Date ?? item.CorrectedDate ?? item.OcrDate ?? DateTime.Today;
        var description = request.Description ?? item.CorrectedSupplier ?? item.OcrSupplier ?? item.FileName;

        return new Transaction
        {
            Date = date,
            Description = description,
            CategoryId = request.CategoryId ?? item.AiSuggestedCategoryId ?? ObjectId.Empty,
            ExpenseAmount = amount,
            PaymentMethod = PaymentMethod.Other,
            Reference = item.CorrectedInvoiceNumber ?? item.OcrInvoiceNumber ?? string.Empty,
            Notes = $"Created from receipt import ({item.Source})",
            FinancialYearId = _financialYearService.GetActiveYear().Id,
            CreatedByUserId = user?.Id ?? ObjectId.Empty,
            CreatedByName = user?.DisplayName ?? "System"
        };
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).StartsWith("._", StringComparison.Ordinal)) continue;
                    if (file.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
                    yield return file;
                }
            }
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                foreach (var extracted in ExtractZip(path))
                    yield return extracted;
            }
            else
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> ExtractZip(string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NickeltownFinance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
        foreach (var file in Directory.EnumerateFiles(tempDir, "*.*", SearchOption.AllDirectories))
            yield return file;
    }

    private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore locked files
        }
    }
}
