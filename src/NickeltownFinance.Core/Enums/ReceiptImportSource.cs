namespace NickeltownFinance.Core.Enums;

/// <summary>
/// How a receipt entered the import inbox.
/// </summary>
public enum ReceiptImportSource
{
    Desktop = 0,
    Mobile = 1,
    Scanner = 2,
    Email = 10,
    Cloud = 11,
    Other = 99
}
