using LiteDB;
using Microsoft.Extensions.Logging;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptProcessingLogger : IReceiptProcessingLogger
{
    private readonly ILogger<ReceiptProcessingLogger> _logger;

    public ReceiptProcessingLogger(ILogger<ReceiptProcessingLogger> logger) => _logger = logger;

    public void LogStageStart(ObjectId importItemId, string stage, string? sourcePath = null) =>
        _logger.LogInformation(
            "Receipt {ImportItemId} stage {Stage} started path={SourcePath}",
            importItemId,
            stage,
            sourcePath ?? "(none)");

    public void LogStageFinish(
        ObjectId importItemId,
        string stage,
        int durationMs,
        byte? confidence = null,
        string? sourcePath = null) =>
        _logger.LogInformation(
            "Receipt {ImportItemId} stage {Stage} finished in {DurationMs}ms confidence={Confidence} path={SourcePath}",
            importItemId,
            stage,
            durationMs,
            confidence,
            sourcePath ?? "(none)");

    public void LogStageError(
        ObjectId importItemId,
        string stage,
        string error,
        int? durationMs = null,
        string? sourcePath = null) =>
        _logger.LogError(
            "Receipt {ImportItemId} stage {Stage} failed after {DurationMs}ms path={SourcePath}: {Error}",
            importItemId,
            stage,
            durationMs,
            sourcePath ?? "(none)",
            error);

    public void LogStageError(
        ObjectId importItemId,
        string stage,
        Exception exception,
        int? durationMs = null,
        string? sourcePath = null) =>
        _logger.LogError(
            exception,
            "Receipt {ImportItemId} stage {Stage} failed after {DurationMs}ms path={SourcePath}",
            importItemId,
            stage,
            durationMs,
            sourcePath ?? "(none)");
}
