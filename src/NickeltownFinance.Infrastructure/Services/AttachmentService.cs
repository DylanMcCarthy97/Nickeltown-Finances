using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif", ".heic"
    };

    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ISessionService _sessionService;
    private readonly IOcrService _ocrService;
    private readonly IDocumentPreviewService _documentPreviewService;

    public AttachmentService(
        IAttachmentRepository attachmentRepository,
        ISessionService sessionService,
        IOcrService ocrService,
        IDocumentPreviewService documentPreviewService)
    {
        _attachmentRepository = attachmentRepository;
        _sessionService = sessionService;
        _ocrService = ocrService;
        _documentPreviewService = documentPreviewService;
    }

    public IReadOnlyList<string> SupportedExtensions => Supported.OrderBy(x => x).ToList();

    public bool IsSupportedFile(string fileName) =>
        Supported.Contains(Path.GetExtension(fileName));

    public Task<IReadOnlyList<AttachmentInfo>> GetForTransactionAsync(ObjectId transactionId)
    {
        var items = _attachmentRepository.GetByTransaction(transactionId)
            .Select(Map)
            .ToList();
        return Task.FromResult<IReadOnlyList<AttachmentInfo>>(items);
    }

    public async Task<AttachmentInfo> AddAsync(ObjectId transactionId, string sourceFilePath, AttachmentKind kind)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("File not found.", sourceFilePath);

        var fileName = Path.GetFileName(sourceFilePath);
        if (!IsSupportedFile(fileName))
            throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(fileName)}");

        var data = await File.ReadAllBytesAsync(sourceFilePath);
        return await AddFromBytesAsync(transactionId, data, fileName, kind);
    }

    public async Task<AttachmentInfo> AddFromBytesAsync(
        ObjectId transactionId,
        byte[] data,
        string fileName,
        AttachmentKind kind)
    {
        if (!IsSupportedFile(fileName))
            throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(fileName)}");

        Directory.CreateDirectory(AppPaths.AttachmentsPath);
        Directory.CreateDirectory(AppPaths.ThumbnailsPath);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullDir = Path.Combine(AppPaths.AttachmentsPath, transactionId.ToString());
        Directory.CreateDirectory(fullDir);
        var fullPath = Path.Combine(fullDir, storedName);
        await File.WriteAllBytesAsync(fullPath, data);

        var relativePath = $"files/attachments/{transactionId}/{storedName}";
        var user = _sessionService.CurrentUser;
        var attachment = new Attachment
        {
            TransactionId = transactionId,
            Kind = kind,
            FileName = fileName,
            RelativePath = relativePath,
            ContentType = GuessContentType(ext),
            SizeBytes = data.LongLength,
            ThumbnailRelativePath = null,
            AddedByUserId = user?.Id ?? ObjectId.Empty,
            AddedByName = user?.DisplayName ?? "System",
            DateAdded = DateTime.UtcNow
        };

        _attachmentRepository.Insert(attachment);
        await ApplyPreviewsAsync(attachment, fullPath);

        if (_ocrService.IsAvailable)
        {
            try
            {
                var candidates = BuildOcrCandidates(attachment, fullPath);
                var ocr = await _ocrService.ExtractBestAsync(candidates);
                if (ocr is not null)
                {
                    ApplyOcr(attachment, ocr);
                    _attachmentRepository.Update(attachment);
                }
            }
            catch
            {
                // OCR is optional
            }
        }

        return Map(attachment);
    }

    public async Task<AttachmentInfo> AddFromInboxAsync(
        ObjectId transactionId,
        string sourceFilePath,
        string fileName,
        AttachmentKind kind,
        ReceiptImportItem importItem)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("File not found.", sourceFilePath);

        var data = await File.ReadAllBytesAsync(sourceFilePath);
        Directory.CreateDirectory(AppPaths.AttachmentsPath);
        Directory.CreateDirectory(AppPaths.ThumbnailsPath);

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullDir = Path.Combine(AppPaths.AttachmentsPath, transactionId.ToString());
        Directory.CreateDirectory(fullDir);
        var fullPath = Path.Combine(fullDir, storedName);
        await File.WriteAllBytesAsync(fullPath, data);

        var relativePath = $"files/attachments/{transactionId}/{storedName}";
        var user = _sessionService.CurrentUser;
        var attachment = new Attachment
        {
            TransactionId = transactionId,
            Kind = kind,
            FileName = fileName,
            RelativePath = relativePath,
            ContentType = GuessContentType(ext),
            SizeBytes = data.LongLength,
            ThumbnailRelativePath = importItem.ThumbnailRelativePath,
            PageCount = importItem.PageCount > 0 ? importItem.PageCount : 1,
            PreviewPagesJson = importItem.PreviewPagesJson,
            AddedByUserId = user?.Id ?? ObjectId.Empty,
            AddedByName = user?.DisplayName ?? "System",
            DateAdded = DateTime.UtcNow,
            FileHash = importItem.FileHash,
            UploadSource = importItem.Source,
            UploadDeviceName = importItem.UploadDeviceName,
            OriginalRelativePath = importItem.OriginalRelativePath,
            OcrProcessed = importItem.OcrProcessed,
            OcrSupplier = importItem.OcrSupplier,
            OcrSupplierConfidence = importItem.OcrSupplierConfidence,
            OcrDate = importItem.OcrDate,
            OcrDateConfidence = importItem.OcrDateConfidence,
            OcrInvoiceNumber = importItem.OcrInvoiceNumber,
            OcrAbn = importItem.OcrAbn,
            OcrSubtotal = importItem.OcrSubtotal,
            OcrGst = importItem.OcrGst,
            OcrTotal = importItem.OcrTotal,
            OcrPaymentMethod = importItem.OcrPaymentMethod,
            OcrSuggestedCategoryId = importItem.AiSuggestedCategoryId,
            OcrCategoryConfidence = importItem.AiCategoryConfidence
        };

        _attachmentRepository.Insert(attachment);

        if (string.IsNullOrWhiteSpace(attachment.PreviewPagesJson))
            await ApplyPreviewsAsync(attachment, fullPath);

        return Map(attachment);
    }

    private async Task ApplyPreviewsAsync(Attachment attachment, string fullPath)
    {
        try
        {
            var previews = await _documentPreviewService.BuildAttachmentPreviewsAsync(
                attachment.TransactionId,
                attachment.Id,
                fullPath);

            attachment.PageCount = Math.Max(1, previews.PageCount);
            attachment.ThumbnailRelativePath = previews.ThumbnailRelativePath ?? attachment.ThumbnailRelativePath;
            attachment.OcrImageRelativePath = previews.OcrImageRelativePath ?? attachment.OcrImageRelativePath;
            if (previews.PreviewRelativePaths.Count > 0)
                attachment.PreviewPagesJson = DocumentPreviewPaths.Serialize(previews.PreviewRelativePaths.ToList());

            _attachmentRepository.Update(attachment);
        }
        catch
        {
            // previews are optional; original is always kept
        }
    }

    private static List<string> BuildOcrCandidates(Attachment attachment, string fullPath)
    {
        var paths = new List<string> { fullPath };

        foreach (var relative in DocumentPreviewPaths.Parse(attachment.PreviewPagesJson))
        {
            var resolved = AppPaths.ResolvePath(relative);
            if (File.Exists(resolved))
                paths.Insert(0, resolved);
        }

        if (!string.IsNullOrWhiteSpace(attachment.OcrImageRelativePath))
        {
            var ocrPath = AppPaths.ResolvePath(attachment.OcrImageRelativePath);
            if (File.Exists(ocrPath))
                paths.Add(ocrPath);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ApplyOcr(Attachment attachment, OcrExtractionResult ocr)
    {
        attachment.OcrProcessed = true;
        attachment.OcrSupplier = ocr.Supplier;
        attachment.OcrSupplierConfidence = ocr.SupplierConfidence;
        attachment.OcrDate = ocr.Date;
        attachment.OcrDateConfidence = ocr.DateConfidence;
        attachment.OcrInvoiceNumber = ocr.InvoiceNumber;
        attachment.OcrAbn = ocr.Abn;
        attachment.OcrSubtotal = ocr.Subtotal;
        attachment.OcrGst = ocr.Gst;
        attachment.OcrTotal = ocr.Total;
        attachment.OcrPaymentMethod = ocr.PaymentMethod;
        attachment.OcrSuggestedCategoryId = ocr.SuggestedCategoryId;
        attachment.OcrCategoryConfidence = ocr.CategoryConfidence;
    }

    public Task DeleteAsync(ObjectId attachmentId)
    {
        var attachment = _attachmentRepository.GetById(attachmentId);
        if (attachment is null) return Task.CompletedTask;

        TryDelete(AppPaths.ResolvePath(attachment.RelativePath));
        if (!string.IsNullOrWhiteSpace(attachment.ThumbnailRelativePath))
            TryDelete(AppPaths.ResolvePath(attachment.ThumbnailRelativePath));

        var previewDir = DocumentPreviewPaths.GetAttachmentPreviewDirectory(attachment.TransactionId, attachment.Id);
        TryDeleteDirectory(previewDir);

        _attachmentRepository.Delete(attachmentId);
        return Task.CompletedTask;
    }

    public Task<string> GetFullPathAsync(ObjectId attachmentId)
    {
        var attachment = _attachmentRepository.GetById(attachmentId)
            ?? throw new InvalidOperationException("Attachment not found.");
        return Task.FromResult(AppPaths.ResolvePath(attachment.RelativePath));
    }

    private AttachmentInfo Map(Attachment attachment)
    {
        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        var previewPaths = DocumentPreviewPaths.Parse(attachment.PreviewPagesJson)
            .Select(AppPaths.ResolvePath)
            .Where(File.Exists)
            .ToList();

        return new AttachmentInfo
        {
            Id = attachment.Id,
            TransactionId = attachment.TransactionId,
            Kind = attachment.Kind,
            FileName = attachment.FileName,
            FullPath = AppPaths.ResolvePath(attachment.RelativePath),
            ThumbnailFullPath = string.IsNullOrWhiteSpace(attachment.ThumbnailRelativePath)
                ? null
                : AppPaths.ResolvePath(attachment.ThumbnailRelativePath),
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            DateAdded = attachment.DateAdded,
            AddedByName = attachment.AddedByName,
            IsImage = ImageExtensions.Contains(ext) || previewPaths.Count > 0,
            IsPdf = ext == ".pdf",
            PageCount = attachment.PageCount > 0 ? attachment.PageCount : Math.Max(1, previewPaths.Count),
            PreviewFullPaths = previewPaths,
            OcrImageFullPath = string.IsNullOrWhiteSpace(attachment.OcrImageRelativePath)
                ? null
                : AppPaths.ResolvePath(attachment.OcrImageRelativePath)
        };
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignore locked files
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore locked files
        }
    }
}
