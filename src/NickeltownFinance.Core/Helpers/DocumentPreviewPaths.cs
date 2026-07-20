using System.Text.Json;
using NickeltownFinance.Core.Constants;

namespace NickeltownFinance.Core.Helpers;

public static class DocumentPreviewPaths
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<string> relativePaths) =>
        JsonSerializer.Serialize(relativePaths, JsonOptions);

    public static string GetInboxPagePath(LiteDB.ObjectId itemId, int pageNumber) =>
        Path.Combine(ReceiptPathHelper.GetItemDirectory(itemId), $"page{pageNumber}.jpg");

    public static string GetAttachmentPreviewDirectory(LiteDB.ObjectId transactionId, LiteDB.ObjectId attachmentId) =>
        Path.Combine(AppPaths.AttachmentsPath, transactionId.ToString(), "previews", attachmentId.ToString());

    public static string GetAttachmentPagePath(LiteDB.ObjectId transactionId, LiteDB.ObjectId attachmentId, int pageNumber) =>
        Path.Combine(GetAttachmentPreviewDirectory(transactionId, attachmentId), $"page{pageNumber}.jpg");

    public static string GetAttachmentThumbnailPath(LiteDB.ObjectId transactionId, LiteDB.ObjectId attachmentId) =>
        Path.Combine(GetAttachmentPreviewDirectory(transactionId, attachmentId), "thumbnail.jpg");

    public static string GetMonthDocumentPreviewDirectory(LiteDB.ObjectId documentId) =>
        Path.Combine(AppPaths.MonthDocumentsPath, documentId.ToString(), "previews");

    /// <summary>Written after PDF pages are rendered with white-background compositing.</summary>
    public const string MonthDocumentRenderVersionMarker = ".render-v2-whitebg";

    public static string GetMonthDocumentPagePath(LiteDB.ObjectId documentId, int pageNumber) =>
        Path.Combine(GetMonthDocumentPreviewDirectory(documentId), $"page{pageNumber}.jpg");
}
