using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Core.Interfaces;

public interface IPdfRenderService
{
    bool IsAvailable { get; }

    int GetPageCount(string pdfPath);

    Task<string?> RenderPageAsync(
        string pdfPath,
        string outputFilePath,
        int pageIndex,
        int renderWidth = 1800,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> RenderAllPagesAsync(
        string pdfPath,
        string outputDirectory,
        int renderWidth = 1800,
        int? maxPages = null,
        CancellationToken cancellationToken = default);
}

public interface IDocumentPreviewService
{
    Task<DocumentPreviewResult> BuildInboxPreviewsAsync(
        ReceiptImportItem item,
        string originalPath,
        bool autoEnhance,
        IList<string> warnings,
        CancellationToken cancellationToken = default);

    Task<DocumentPreviewResult> BuildAttachmentPreviewsAsync(
        ObjectId transactionId,
        ObjectId attachmentId,
        string originalPath,
        CancellationToken cancellationToken = default);
}
