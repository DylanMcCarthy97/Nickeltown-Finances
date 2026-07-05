namespace NickeltownFinance.Core.Enums;

/// <summary>
/// Where a receipt import should appear and how it is resolved.
/// </summary>
public enum ReceiptImportTarget
{
    /// <summary>General Receipt Inbox (mobile from imports, desktop browse, drag/drop).</summary>
    Inbox = 0,

    /// <summary>Attached directly to a transaction from Edit/Add Expense mobile upload.</summary>
    Transaction = 1
}
