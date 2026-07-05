using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.Document;

public sealed class DocumentPreviewService : IDocumentPreviewService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tif", ".tiff"
    };

    private readonly IPdfRenderService _pdfRenderService;
    private readonly IReceiptImageProcessor _imageProcessor;
    private readonly IReceiptThumbnailService _thumbnailService;

    public DocumentPreviewService(
        IPdfRenderService pdfRenderService,
        IReceiptImageProcessor imageProcessor,
        IReceiptThumbnailService thumbnailService)
    {
        _pdfRenderService = pdfRenderService;
        _imageProcessor = imageProcessor;
        _thumbnailService = thumbnailService;
    }

    public async Task<DocumentPreviewResult> BuildInboxPreviewsAsync(
        ReceiptImportItem item,
        string originalPath,
        bool autoEnhance,
        IList<string> warnings,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalPath).ToLowerInvariant();
        if (ext == ".pdf")
            return await BuildPdfInboxPreviewsAsync(item, originalPath, warnings, cancellationToken);

        if (!ImageExtensions.Contains(ext))
            return new DocumentPreviewResult { PageCount = 1 };

        return await BuildImageInboxPreviewsAsync(item, originalPath, autoEnhance, warnings, cancellationToken);
    }

    public async Task<DocumentPreviewResult> BuildAttachmentPreviewsAsync(
        ObjectId transactionId,
        ObjectId attachmentId,
        string originalPath,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalPath).ToLowerInvariant();
        var previewDir = DocumentPreviewPaths.GetAttachmentPreviewDirectory(transactionId, attachmentId);
        Directory.CreateDirectory(previewDir);

        if (ext == ".pdf")
        {
            var rendered = await _pdfRenderService.RenderAllPagesAsync(
                originalPath,
                previewDir,
                cancellationToken: cancellationToken);

            if (rendered.Count == 0)
                return new DocumentPreviewResult { PageCount = _pdfRenderService.GetPageCount(originalPath) };

            var relativePaths = rendered.Select(AppPaths.ToRelativePath).ToList();
            var thumbPath = DocumentPreviewPaths.GetAttachmentThumbnailPath(transactionId, attachmentId);
            await _thumbnailService.GenerateAsync(rendered[0], thumbPath);

            return new DocumentPreviewResult
            {
                PageCount = rendered.Count,
                PrimaryPreviewRelativePath = relativePaths[0],
                ThumbnailRelativePath = File.Exists(thumbPath) ? AppPaths.ToRelativePath(thumbPath) : relativePaths[0],
                PreviewRelativePaths = relativePaths
            };
        }

        if (!ImageExtensions.Contains(ext))
            return new DocumentPreviewResult { PageCount = 1 };

        var previewPath = Path.Combine(previewDir, "preview.jpg");
        var ocrPath = Path.Combine(previewDir, "ocr.jpg");
        return await BuildImageOutputsAsync(originalPath, previewPath, ocrPath, autoEnhance: true, warnings: []);
    }

    private async Task<DocumentPreviewResult> BuildPdfInboxPreviewsAsync(
        ReceiptImportItem item,
        string originalPath,
        IList<string> warnings,
        CancellationToken cancellationToken)
    {
        var outputDir = ReceiptPathHelper.GetItemDirectory(item.Id);
        Directory.CreateDirectory(outputDir);

        var rendered = await _pdfRenderService.RenderAllPagesAsync(
            originalPath,
            outputDir,
            cancellationToken: cancellationToken);

        if (rendered.Count == 0)
        {
            warnings.Add("PDF preview generation failed; original PDF preserved.");
            return new DocumentPreviewResult { PageCount = _pdfRenderService.GetPageCount(originalPath) };
        }

        var relativePaths = rendered.Select(AppPaths.ToRelativePath).ToList();
        var thumbPath = ReceiptPathHelper.GetThumbnailPath(item.Id);
        await _thumbnailService.GenerateAsync(rendered[0], thumbPath);

        return new DocumentPreviewResult
        {
            PageCount = rendered.Count,
            PrimaryPreviewRelativePath = relativePaths[0],
            ThumbnailRelativePath = File.Exists(thumbPath) ? AppPaths.ToRelativePath(thumbPath) : relativePaths[0],
            PreviewRelativePaths = relativePaths
        };
    }

    private async Task<DocumentPreviewResult> BuildImageInboxPreviewsAsync(
        ReceiptImportItem item,
        string originalPath,
        bool autoEnhance,
        IList<string> warnings,
        CancellationToken cancellationToken)
    {
        var previewPath = ReceiptPathHelper.GetPreviewPath(item.Id);
        var ocrPath = ReceiptPathHelper.GetOcrImagePath(item.Id);
        var result = await BuildImageOutputsAsync(originalPath, previewPath, ocrPath, autoEnhance, warnings);

        var thumbPath = ReceiptPathHelper.GetThumbnailPath(item.Id);
        var previewFull = AppPaths.ResolvePath(result.PrimaryPreviewRelativePath ?? previewPath);
        var thumbResult = await _thumbnailService.GenerateAsync(previewFull, thumbPath, cancellationToken: cancellationToken);
        if (thumbResult is null)
            warnings.Add("Thumbnail generation failed; original receipt preserved.");
        else
            result.ThumbnailRelativePath = AppPaths.ToRelativePath(thumbResult);

        return result;
    }

    private async Task<DocumentPreviewResult> BuildImageOutputsAsync(
        string originalPath,
        string previewPath,
        string ocrPath,
        bool autoEnhance,
        IList<string> warnings)
    {
        if (autoEnhance && _imageProcessor.IsAvailable)
        {
            var previewResult = await _imageProcessor.ProcessPreviewAsync(new ReceiptImageProcessRequest
            {
                SourceFilePath = originalPath,
                OutputFilePath = previewPath
            });

            if (!previewResult.Success || !File.Exists(previewPath))
            {
                warnings.Add("Preview enhancement failed; using original colours.");
                if (!ReceiptPathHelper.TryCopyFile(originalPath, previewPath, out _))
                    throw new InvalidOperationException("Could not create preview image.");
            }

            var ocrResult = await _imageProcessor.ProcessOcrAsync(new ReceiptImageProcessRequest
            {
                SourceFilePath = originalPath,
                OutputFilePath = ocrPath
            });

            if (!ocrResult.Success || !File.Exists(ocrPath))
                warnings.Add("OCR image preparation failed; OCR will use preview or original.");
        }
        else
        {
            if (!ReceiptPathHelper.TryCopyFile(originalPath, previewPath, out var copyError))
                throw new InvalidOperationException(copyError ?? "Could not create preview image.");
        }

        string? ocrRelative = File.Exists(ocrPath) ? AppPaths.ToRelativePath(ocrPath) : null;
        var previewRelative = AppPaths.ToRelativePath(previewPath);

        return new DocumentPreviewResult
        {
            PageCount = 1,
            PrimaryPreviewRelativePath = previewRelative,
            OcrImageRelativePath = ocrRelative,
            PreviewRelativePaths = [previewRelative]
        };
    }
}
