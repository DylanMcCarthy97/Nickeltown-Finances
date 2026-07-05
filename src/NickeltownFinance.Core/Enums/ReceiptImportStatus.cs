namespace NickeltownFinance.Core.Enums;

/// <summary>
/// Pipeline state for a receipt in the import inbox.
/// </summary>
public enum ReceiptImportStatus
{
    Queued = 0,
    Uploading = 1,
    Preprocessing = 2,
    ProcessingOcr = 3,
    SupplierDetection = 6,
    AiParsing = 4,
    MatchingTransaction = 5,
    GeneratingThumbnail = 7,
    Ready = 10,
    CompletedWithWarnings = 11,
    Failed = 20,
    Committed = 30,
    Ignored = 31
}
