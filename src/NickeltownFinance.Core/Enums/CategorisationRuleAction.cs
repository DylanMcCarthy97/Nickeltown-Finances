namespace NickeltownFinance.Core.Enums;

public enum CategorisationRuleAction
{
    /// <summary>Assign a ledger category.</summary>
    AssignCategory = 0,

    /// <summary>Mark as a Square deposit transfer (eligible for auto-match).</summary>
    SquareDeposit = 1
}
