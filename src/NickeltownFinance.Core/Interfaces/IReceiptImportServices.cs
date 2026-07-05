using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

/// <summary>
/// Orchestrates the receipt import inbox: intake, pipeline, and user commit actions.
/// </summary>
public interface IReceiptImportService
{
    Task<IReadOnlyList<ReceiptImportItemInfo>> GetInboxAsync(bool includeCommitted = false);

    Task<ReceiptImportItemInfo?> GetByIdAsync(ObjectId id);

    Task<IReadOnlyList<ReceiptImportItemInfo>> ImportFromDesktopAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default);

    Task<ReceiptImportItemInfo> EnqueueFromUploadAsync(
        byte[] data,
        string fileName,
        string contentType,
        ReceiptImportSource source,
        string? deviceName = null,
        string? userAgent = null,
        ObjectId? pendingTransactionId = null,
        string? uploadSessionKey = null,
        ReceiptImportTarget importTarget = ReceiptImportTarget.Inbox,
        CancellationToken cancellationToken = default);

    Task TryAutoAttachAsync(ObjectId importItemId, CancellationToken cancellationToken = default);

    Task AttachSessionItemsToTransactionAsync(
        string uploadSessionKey,
        ObjectId transactionId,
        CancellationToken cancellationToken = default);

    Task ResolveSessionUploadsOnCancelAsync(
        string uploadSessionKey,
        bool keepInInbox,
        CancellationToken cancellationToken = default);

    Task CommitAsync(ReceiptImportCommitRequest request, CancellationToken cancellationToken = default);

    Task IgnoreAsync(ObjectId importItemId);

    Task RetryAsync(ObjectId importItemId);

    Task DeleteAsync(ObjectId importItemId);

    Task SaveOcrCorrectionsAsync(ReceiptOcrCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptImportItemInfo>> SearchInboxAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Background processing queue for receipt import pipeline stages.
/// </summary>
public interface IReceiptImportQueue
{
    event EventHandler<ReceiptImportItemInfo>? ItemUpdated;

    void Enqueue(ObjectId importItemId);

    void NotifyItemUpdated(ReceiptImportItemInfo item);

    int PendingCount { get; }

    bool IsProcessing { get; }
}

/// <summary>
/// Embedded local-network HTTP server for mobile/PWA receipt upload.
/// </summary>
public interface IMobileUploadHost
{
    bool IsRunning { get; }

    MobileUploadSessionInfo? CurrentSession { get; }

    Task<MobileUploadSessionInfo> StartSessionAsync(
        MobileUploadSessionRequest? request = null,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<MobileUploadSessionInfo> RefreshSessionAsync(
        MobileUploadSessionRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<ReceiptUploadStatusInfo?> GetUploadStatusAsync(ObjectId importItemId);

    /// <summary>Validates token and client IP for upload endpoints.</summary>
    bool TryValidateUpload(string token, string clientIp, out string? failureReason);

    /// <summary>User-facing error message for phone clients.</summary>
    string MapClientError(string? failureReason);
}

/// <summary>
/// Receipt camera pipeline: edge detect, crop, deskew, contrast, white background.
/// </summary>
public interface IReceiptImageProcessor
{
    bool IsAvailable { get; }

    /// <summary>Colour-enhanced preview for user viewing (Adobe Scan style).</summary>
    Task<ReceiptImageProcessResult> ProcessPreviewAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Grayscale/threshold pipeline for OCR only — never shown to users.</summary>
    Task<ReceiptImageProcessResult> ProcessOcrAsync(
        ReceiptImageProcessRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Suggests expense category and enriches OCR fields with confidence scores.
/// </summary>
public interface IReceiptAiParser
{
    bool IsAvailable { get; }

    Task<ReceiptAiSuggestionInfo?> ParseAsync(
        ReceiptImportItem item,
        ReceiptOcrInfo ocr,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds ANZ ledger transactions that may match an imported receipt.
/// </summary>
public interface IReceiptMatchingService
{
    Task<ReceiptMatchSuggestionInfo?> FindBestMatchAsync(
        ReceiptImportItem item,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptMatchSuggestionInfo>> FindMatchesAsync(
        ObjectId importItemId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// TWAIN/WIA scanner integration for direct scan-to-inbox.
/// </summary>
public interface IScannerService
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<ScannerDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<ScannerCaptureResult> ScanAsync(
        ScannerCaptureRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Batch undo for grouped desktop/scanner import sessions.
/// </summary>
public interface IReceiptImportBatchService
{
    Task<ReceiptImportBatch> CreateBatchAsync(ReceiptImportSource source, string label);

    Task UndoBatchAsync(ObjectId batchId);
}
