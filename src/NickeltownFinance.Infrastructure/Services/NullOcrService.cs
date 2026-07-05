using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services;

/// <summary>
/// Placeholder OCR provider for Version 2. Always unavailable; never mutates user data.
/// </summary>
public sealed class NullOcrService : IOcrService
{
    public bool IsAvailable => false;

    public Task<OcrExtractionResult?> ExtractAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult<OcrExtractionResult?>(null);

    public Task<OcrExtractionResult?> ExtractBestAsync(
        IReadOnlyList<string> candidatePaths,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<OcrExtractionResult?>(null);
}
