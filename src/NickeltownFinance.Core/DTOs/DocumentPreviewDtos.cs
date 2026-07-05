namespace NickeltownFinance.Core.DTOs;

public class DocumentPreviewResult
{
    public int PageCount { get; set; } = 1;

    public string? PrimaryPreviewRelativePath { get; set; }

    public string? OcrImageRelativePath { get; set; }

    public string? ThumbnailRelativePath { get; set; }

    public IReadOnlyList<string> PreviewRelativePaths { get; set; } = [];
}
