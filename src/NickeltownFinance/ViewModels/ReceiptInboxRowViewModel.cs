using NickeltownFinance.Core.DTOs;

using NickeltownFinance.Core.Enums;



namespace NickeltownFinance.ViewModels;



public sealed class ReceiptInboxRowViewModel

{

    public ReceiptInboxRowViewModel(ReceiptImportItemInfo source)

    {

        Source = source;

    }



    public ReceiptImportItemInfo Source { get; }



    public ReceiptImportStatus Status => Source.Status;



    public string FileName => Source.FileName;



    public string DateDisplay => Source.Ocr?.EffectiveDate?.ToString("dd/MM/yyyy") ?? "—";



    public string TotalDisplay => Source.Ocr?.EffectiveTotal?.ToString("C") ?? "—";



    public string GstDisplay => Source.Ocr?.EffectiveGst?.ToString("C") ?? "—";



    public string CategoryDisplay => Source.AiSuggestion?.CategoryName ?? "—";



    public string ConfidenceDisplay

    {

        get

        {

            var parts = new List<string>();

            if (Source.Ocr?.SupplierConfidence is { } sc)

                parts.Add($"Supplier {sc}%");

            if (Source.AiSuggestion?.Confidence is { } cc)

                parts.Add($"Category {cc}%");

            if (Source.MatchSuggestion is { } ms)

                parts.Add($"{ms.QualityDisplay} {ms.Confidence}%");

            return parts.Count == 0 ? "—" : string.Join(" · ", parts);

        }

    }



    public string SourceDisplay => Source.Source switch

    {

        ReceiptImportSource.Mobile => "Mobile",

        ReceiptImportSource.Desktop => "Desktop",

        ReceiptImportSource.Scanner => "Scanner",

        _ => Source.Source.ToString()

    };



    public string StatusLabel => Status switch

    {

        ReceiptImportStatus.Committed => "Matched",

        ReceiptImportStatus.CompletedWithWarnings => "Warnings",

        ReceiptImportStatus.Ready when Source.MatchSuggestion is null => "Needs review",

        ReceiptImportStatus.Failed => "Needs review",

        _ => Source.StatusDisplay

    };



    public string UploadedTimeDisplay => Source.CreatedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");



    public string ThumbnailPath

    {

        get

        {

            if (!string.IsNullOrWhiteSpace(Source.ThumbnailFullPath) && File.Exists(Source.ThumbnailFullPath))

                return Source.ThumbnailFullPath;

            return PreviewPath;

        }

    }



    public string PreviewPath

    {

        get

        {

            if (Source.PreviewFullPaths.Count > 0 && File.Exists(Source.PreviewFullPaths[0]))

                return Source.PreviewFullPaths[0];



            if (!string.IsNullOrWhiteSpace(Source.ProcessedFullPath) && File.Exists(Source.ProcessedFullPath))

                return Source.ProcessedFullPath;



            return Source.OriginalFullPath;

        }

    }



    public bool IsImage

    {

        get

        {

            if (Source.PreviewFullPaths.Count > 0)

                return true;



            var ext = Path.GetExtension(PreviewPath).ToLowerInvariant();

            return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".tif" or ".tiff" or ".heic";

        }

    }



    public bool IsPdf =>

        Path.GetExtension(Source.OriginalFullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)

        && Source.PreviewFullPaths.Count == 0;



    public bool IsProcessing => Status is ReceiptImportStatus.Queued

        or ReceiptImportStatus.Uploading

        or ReceiptImportStatus.Preprocessing

        or ReceiptImportStatus.ProcessingOcr

        or ReceiptImportStatus.SupplierDetection

        or ReceiptImportStatus.AiParsing

        or ReceiptImportStatus.MatchingTransaction

        or ReceiptImportStatus.GeneratingThumbnail;



    public bool IsNeedsReview => Status is ReceiptImportStatus.Ready

        or ReceiptImportStatus.Failed

        or ReceiptImportStatus.CompletedWithWarnings;



    public bool CanAct => Status is not ReceiptImportStatus.Committed and not ReceiptImportStatus.Ignored;

}

