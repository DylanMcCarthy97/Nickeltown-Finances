using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

public interface ISupplierDetectionService
{
    Task<SupplierDetectionResult?> DetectAsync(
        ReceiptImportItem item,
        ReceiptOcrInfo ocr,
        CancellationToken cancellationToken = default);

    Task RecordPurchaseAsync(
        ObjectId profileId,
        decimal amount,
        DateTime purchaseDate,
        ObjectId? categoryId = null,
        CancellationToken cancellationToken = default);
}

public interface IReceiptThumbnailService
{
    Task<string?> GenerateAsync(
        string sourceFilePath,
        string outputFilePath,
        int maxWidth = 120,
        CancellationToken cancellationToken = default);
}

public interface IReceiptDuplicateDetector
{
    Task<ReceiptDuplicateCheckResult> CheckAsync(
        ReceiptImportItem item,
        CancellationToken cancellationToken = default);
}

public interface IReceiptProcessingSettingsService
{
    ReceiptProcessingSettingsInfo GetSettings();

    void SaveSettings(ReceiptProcessingSettingsInfo settings);
}

public interface IReceiptProcessingLogger
{
    void LogStageStart(ObjectId importItemId, string stage, string? sourcePath = null);

    void LogStageFinish(
        ObjectId importItemId,
        string stage,
        int durationMs,
        byte? confidence = null,
        string? sourcePath = null);

    void LogStageError(
        ObjectId importItemId,
        string stage,
        string error,
        int? durationMs = null,
        string? sourcePath = null);

    void LogStageError(
        ObjectId importItemId,
        string stage,
        Exception exception,
        int? durationMs = null,
        string? sourcePath = null);
}

public interface IReceiptOcrFieldParser
{
    OcrExtractionResult Parse(string fullText, IReadOnlyDictionary<string, byte>? lineConfidences = null);
}
