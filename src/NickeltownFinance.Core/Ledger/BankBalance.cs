namespace NickeltownFinance.Core.Ledger;

/// <summary>
/// Bank ledger balance as of a calendar day, using the club tracking start.
/// Tracking balance is end-of-day on the starting date (includes that day's transactions).
/// </summary>
public static class BankBalance
{
    public static decimal AsOf(
        DateTime asOfDate,
        DateTime trackingDate,
        decimal trackingBalance,
        IEnumerable<(DateTime Date, decimal NetAmount)> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var asOf = asOfDate.Date;
        trackingDate = trackingDate.Date;

        if (asOf == trackingDate)
            return trackingBalance;

        decimal net = 0;
        foreach (var txn in transactions)
        {
            var day = txn.Date.Date;
            if (asOf < trackingDate)
            {
                // Working backwards: remove activity from after asOf through the starting date.
                if (day > asOf && day <= trackingDate)
                    net += txn.NetAmount;
            }
            else if (day > trackingDate && day <= asOf)
            {
                // After tracking start: only activity strictly after the starting date.
                net += txn.NetAmount;
            }
        }

        return asOf < trackingDate
            ? trackingBalance - net
            : trackingBalance + net;
    }
}
