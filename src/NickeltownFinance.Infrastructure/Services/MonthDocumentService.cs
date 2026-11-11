using System.Globalization;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Reports.Exporters;

namespace NickeltownFinance.Infrastructure.Services;

public class MonthDocumentService : IMonthDocumentService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tiff", ".tif"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".tiff", ".tif", ".heic"
    };

    private readonly IMonthDocumentRepository _repository;
    private readonly ISessionService _sessionService;
    private readonly ISettingsService _settingsService;
    private readonly IPdfRenderService _pdfRenderService;

    public MonthDocumentService(
        IMonthDocumentRepository repository,
        ISessionService sessionService,
        ISettingsService settingsService,
        IPdfRenderService pdfRenderService)
    {
        _repository = repository;
        _sessionService = sessionService;
        _settingsService = settingsService;
        _pdfRenderService = pdfRenderService;
    }

    public IReadOnlyList<string> SupportedExtensions => Supported.OrderBy(x => x).ToList();

    public bool IsSupportedFile(string fileName) =>
        Supported.Contains(Path.GetExtension(fileName));

    public async Task<IReadOnlyList<MonthDocumentInfo>> GetForMonthAsync(
        int year,
        int month,
        MonthDocumentKind kind = MonthDocumentKind.PitstopReport)
    {
        var documents = _repository.GetForMonth(year, month, kind).ToList();
        foreach (var document in documents)
            await EnsurePreviewsAsync(document);

        return documents.Select(Map).ToList();
    }

    public async Task<MonthDocumentInfo> AddAsync(
        int year,
        int month,
        string sourceFilePath,
        MonthDocumentKind kind,
        string? title = null)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("File not found.", sourceFilePath);

        var fileName = Path.GetFileName(sourceFilePath);
        if (!IsSupportedFile(fileName))
            throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(fileName)}");

        var data = await File.ReadAllBytesAsync(sourceFilePath);
        return await AddFromBytesAsync(year, month, data, fileName, kind, title);
    }

    public async Task<MonthDocumentInfo> AddFromBytesAsync(
        int year,
        int month,
        byte[] data,
        string fileName,
        MonthDocumentKind kind,
        string? title = null)
    {
        if (!IsSupportedFile(fileName))
            throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(fileName)}");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var document = new MonthDocument
        {
            Year = year,
            Month = month,
            Kind = kind,
            FileName = fileName,
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim(),
            ContentType = GuessContentType(ext),
            SizeBytes = data.LongLength,
            AddedByUserId = _sessionService.CurrentUser?.Id ?? ObjectId.Empty,
            AddedByName = _sessionService.CurrentUser?.DisplayName ?? "System",
            DateAdded = DateTime.UtcNow
        };

        _repository.Insert(document);

        var fullDir = Path.Combine(AppPaths.MonthDocumentsPath, document.Id.ToString());
        Directory.CreateDirectory(fullDir);
        var fullPath = Path.Combine(fullDir, storedName);
        await File.WriteAllBytesAsync(fullPath, data);

        document.RelativePath = AppPaths.ToRelativePath(fullPath);
        await ApplyPreviewsAsync(document, fullPath);
        _repository.Update(document);

        return Map(document);
    }

    public async Task<MonthDocumentInfo> AddManualPitstopAsync(
        int year,
        int month,
        DateTime eventDate,
        decimal cashAmount,
        decimal cardAmount,
        string? title = null,
        string? notes = null)
    {
        ValidateManualPitstop(year, month, eventDate, cashAmount, cardAmount);

        var resolvedTitle = ResolveManualTitle(title, eventDate);
        var fileName = ManualFileName(eventDate);
        var pdf = BuildManualPdf(resolvedTitle, eventDate, cashAmount, cardAmount, notes);
        var info = await AddFromBytesAsync(year, month, pdf, fileName, MonthDocumentKind.PitstopReport, resolvedTitle);

        var document = _repository.GetById(info.Id)
            ?? throw new InvalidOperationException("Manual Pitstop entry could not be saved.");
        ApplyManualFields(document, eventDate, cashAmount, cardAmount, notes);
        _repository.Update(document);
        return Map(document);
    }

    public async Task<MonthDocumentInfo> UpdateManualPitstopAsync(
        ObjectId documentId,
        DateTime eventDate,
        decimal cashAmount,
        decimal cardAmount,
        string? title = null,
        string? notes = null)
    {
        var document = _repository.GetById(documentId)
            ?? throw new InvalidOperationException("Pitstop entry not found.");
        if (!document.IsManual)
            throw new InvalidOperationException("Only manual Pitstop entries can be edited.");

        ValidateManualPitstop(document.Year, document.Month, eventDate, cashAmount, cardAmount);

        var resolvedTitle = ResolveManualTitle(title, eventDate);
        var pdf = BuildManualPdf(resolvedTitle, eventDate, cashAmount, cardAmount, notes);
        var fullPath = string.IsNullOrWhiteSpace(document.RelativePath)
            ? string.Empty
            : AppPaths.ResolvePath(document.RelativePath);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            var storedName = $"{Guid.NewGuid():N}.pdf";
            var fullDir = Path.Combine(AppPaths.MonthDocumentsPath, document.Id.ToString());
            Directory.CreateDirectory(fullDir);
            fullPath = Path.Combine(fullDir, storedName);
            document.RelativePath = AppPaths.ToRelativePath(fullPath);
        }
        else
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, pdf);

        document.Title = resolvedTitle;
        document.FileName = ManualFileName(eventDate);
        document.SizeBytes = pdf.LongLength;
        document.ContentType = "application/pdf";
        document.ModifiedDate = DateTime.UtcNow;
        ApplyManualFields(document, eventDate, cashAmount, cardAmount, notes);
        await ApplyPreviewsAsync(document, fullPath);
        _repository.Update(document);
        return Map(document);
    }

    public Task DeleteAsync(ObjectId documentId)
    {
        var document = _repository.GetById(documentId);
        if (document is null)
            return Task.CompletedTask;

        TryDelete(AppPaths.ResolvePath(document.RelativePath));
        if (!string.IsNullOrWhiteSpace(document.ThumbnailRelativePath))
            TryDelete(AppPaths.ResolvePath(document.ThumbnailRelativePath));

        TryDeleteDirectory(DocumentPreviewPaths.GetMonthDocumentPreviewDirectory(document.Id));
        TryDeleteDirectory(Path.Combine(AppPaths.MonthDocumentsPath, document.Id.ToString()));

        _repository.Delete(documentId);
        return Task.CompletedTask;
    }

    public Task<string> GetFullPathAsync(ObjectId documentId)
    {
        var document = _repository.GetById(documentId)
            ?? throw new InvalidOperationException("Document not found.");
        return Task.FromResult(AppPaths.ResolvePath(document.RelativePath));
    }

    public AttachmentInfo ToAttachmentInfo(MonthDocumentInfo document) =>
        new()
        {
            Id = document.Id,
            Kind = AttachmentKind.Other,
            FileName = document.FileName,
            FullPath = document.FullPath,
            ThumbnailFullPath = document.ThumbnailFullPath,
            ContentType = document.ContentType,
            SizeBytes = document.SizeBytes,
            DateAdded = document.DateAdded,
            AddedByName = document.AddedByName,
            IsImage = document.IsImage,
            IsPdf = document.IsPdf,
            PageCount = document.PageCount,
            PreviewFullPaths = document.PreviewFullPaths
        };

    private async Task ApplyPreviewsAsync(MonthDocument document, string fullPath)
    {
        try
        {
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (ext == ".pdf")
            {
                var previewDir = DocumentPreviewPaths.GetMonthDocumentPreviewDirectory(document.Id);
                var rendered = await _pdfRenderService.RenderAllPagesAsync(fullPath, previewDir);
                if (rendered.Count > 0)
                {
                    document.PageCount = rendered.Count;
                    var relativePaths = rendered.Select(AppPaths.ToRelativePath).ToList();
                    document.PreviewPagesJson = DocumentPreviewPaths.Serialize(relativePaths);
                    document.ThumbnailRelativePath = relativePaths[0];
                }
            }
            else if (ImageExtensions.Contains(ext))
            {
                document.PageCount = 1;
                document.ThumbnailRelativePath = document.RelativePath;
                document.PreviewPagesJson = DocumentPreviewPaths.Serialize([document.RelativePath]);
            }
        }
        catch
        {
            // previews are optional
        }
    }

    /// <summary>
    /// Backfills / refreshes page previews for attached documents.
    /// </summary>
    private async Task EnsurePreviewsAsync(MonthDocument document)
    {
        var fullPath = AppPaths.ResolvePath(document.RelativePath);
        if (!File.Exists(fullPath))
            return;

        var existing = DocumentPreviewPaths.Parse(document.PreviewPagesJson)
            .Select(AppPaths.ResolvePath)
            .Where(File.Exists)
            .ToList();

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        if (ext == ".pdf")
        {
            var previewDir = DocumentPreviewPaths.GetMonthDocumentPreviewDirectory(document.Id);
            var versionMarker = Path.Combine(previewDir, DocumentPreviewPaths.MonthDocumentRenderVersionMarker);
            var pageCount = Math.Max(1, _pdfRenderService.GetPageCount(fullPath));
            var previewsCurrent = existing.Count >= pageCount && File.Exists(versionMarker);
            if (previewsCurrent)
            {
                if (document.PageCount != pageCount)
                {
                    document.PageCount = pageCount;
                    _repository.Update(document);
                }
                return;
            }

            await ApplyPreviewsAsync(document, fullPath);
            _repository.Update(document);
            return;
        }

        if (ImageExtensions.Contains(ext) && existing.Count == 0)
        {
            await ApplyPreviewsAsync(document, fullPath);
            _repository.Update(document);
        }
    }

    private static MonthDocumentInfo Map(MonthDocument document)
    {
        var ext = Path.GetExtension(document.FileName).ToLowerInvariant();
        var previewPaths = DocumentPreviewPaths.Parse(document.PreviewPagesJson)
            .Select(AppPaths.ResolvePath)
            .Where(File.Exists)
            .ToList();

        return new MonthDocumentInfo
        {
            Id = document.Id,
            Year = document.Year,
            Month = document.Month,
            Kind = document.Kind,
            Title = string.IsNullOrWhiteSpace(document.Title)
                ? Path.GetFileNameWithoutExtension(document.FileName)
                : document.Title,
            FileName = document.FileName,
            FullPath = AppPaths.ResolvePath(document.RelativePath),
            ThumbnailFullPath = string.IsNullOrWhiteSpace(document.ThumbnailRelativePath)
                ? null
                : AppPaths.ResolvePath(document.ThumbnailRelativePath),
            ContentType = document.ContentType,
            SizeBytes = document.SizeBytes,
            DateAdded = document.DateAdded,
            AddedByName = document.AddedByName,
            IsImage = ImageExtensions.Contains(ext) || previewPaths.Count > 0,
            IsPdf = ext == ".pdf",
            PageCount = document.PageCount > 0 ? document.PageCount : Math.Max(1, previewPaths.Count),
            PreviewFullPaths = previewPaths,
            IsManual = document.IsManual,
            EventDate = document.EventDate,
            CashAmount = document.CashAmount,
            CardAmount = document.CardAmount,
            Notes = document.Notes ?? string.Empty
        };
    }

    private byte[] BuildManualPdf(
        string title,
        DateTime eventDate,
        decimal cashAmount,
        decimal cardAmount,
        string? notes) =>
        PitstopManualEntryPdf.Generate(new PitstopManualEntryPdfRequest
        {
            ClubName = _settingsService.ClubName,
            Title = title,
            EventDate = eventDate.Date,
            CashAmount = cashAmount,
            CardAmount = cardAmount,
            Notes = notes?.Trim() ?? string.Empty,
            RecordedBy = _sessionService.CurrentUser?.DisplayName ?? "Treasurer",
            RecordedAt = DateTime.Now
        });

    private static void ApplyManualFields(
        MonthDocument document,
        DateTime eventDate,
        decimal cashAmount,
        decimal cardAmount,
        string? notes)
    {
        document.IsManual = true;
        document.EventDate = eventDate.Date;
        document.CashAmount = cashAmount;
        document.CardAmount = cardAmount;
        document.Notes = notes?.Trim() ?? string.Empty;
    }

    private static string ResolveManualTitle(string? title, DateTime eventDate)
    {
        var trimmed = title?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Pitstop {eventDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}"
            : trimmed;
    }

    private static string ManualFileName(DateTime eventDate) =>
        $"Pitstop-manual-{eventDate:yyyy-MM-dd}.pdf";

    private static void ValidateManualPitstop(
        int year,
        int month,
        DateTime eventDate,
        decimal cashAmount,
        decimal cardAmount)
    {
        if (year < 2000 || month is < 1 or > 12)
            throw new InvalidOperationException("A valid report month is required.");

        if (eventDate == default)
            throw new InvalidOperationException("Event date is required.");

        if (eventDate.Year != year || eventDate.Month != month)
        {
            var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
            throw new InvalidOperationException($"Event date must be in {monthName}.");
        }

        if (cashAmount < 0 || cardAmount < 0)
            throw new InvalidOperationException("Cash and card amounts cannot be negative.");

        if (cashAmount == 0 && cardAmount == 0)
            throw new InvalidOperationException("Enter a cash amount, a card amount, or both.");
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
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }
}