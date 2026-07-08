namespace NickeltownFinance.Core.Enums;

/// <summary>
/// Identifies the origin of an import batch. New sources (POS, inventory, memberships)
/// can be added without changing the import engine contract.
/// </summary>
public enum ImportSourceType
{
    AnzBank = 0,
    Square = 1,
    Receipt = 2,
    Pos = 10,
    Inventory = 11,
    Membership = 12,
    LegacyTreasurerReport = 13,
    Other = 99
}
