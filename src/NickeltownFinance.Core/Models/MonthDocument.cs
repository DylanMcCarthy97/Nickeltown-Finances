using LiteDB;
using NickeltownFinance.Core.Enums;

namespace NickeltownFinance.Core.Models;

public class MonthDocument : BaseEntity
{
    public int Year { get; set; }

    public int Month { get; set; }

    public MonthDocumentKind Kind { get; set; } = MonthDocumentKind.PitstopReport;

    public string FileName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? ThumbnailRelativePath { get; set; }

    public int PageCount { get; set; } = 1;

    public string? PreviewPagesJson { get; set; }

    public ObjectId AddedByUserId { get; set; } = ObjectId.Empty;

    public string AddedByName { get; set; } = string.Empty;

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}