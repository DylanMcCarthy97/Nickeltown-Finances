using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const double MaxBarHeight = 120;

    private readonly IFinancialYearService _financialYearService;
    private readonly ITransactionService _transactionService;
    private readonly IStatementImportService _importService;

    public DashboardService(
        IFinancialYearService financialYearService,
        ITransactionService transactionService,
        IStatementImportService importService)
    {
        _financialYearService = financialYearService;
        _transactionService = transactionService;
        _importService = importService;
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var fy = _financialYearService.GetActiveYear();
        var ledger = await _transactionService.GetLedgerAsync(fy.Id);
        var active = ledger.Where(x => !x.IsDeleted).ToList();

        var referenceDate = DateTime.Today;
        if (referenceDate > fy.EndDate) referenceDate = fy.EndDate;
        if (referenceDate < fy.OpeningDate) referenceDate = fy.OpeningDate;
        var monthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);

        var incomeMonth = active.Where(x => x.Date >= monthStart && x.Date < monthStart.AddMonths(1)).Sum(x => x.IncomeAmount);
        var expenseMonth = active.Where(x => x.Date >= monthStart && x.Date < monthStart.AddMonths(1)).Sum(x => x.ExpenseAmount);

        // True bank balance = club tracking start balance + activity on/after that date
        // (historical imports before the start date only adjust opening balances).
        var ytdIncome = active.Sum(x => x.IncomeAmount);
        var ytdExpenses = active.Sum(x => x.ExpenseAmount);
        var balance = _financialYearService.GetCurrentBankBalance();

        var chartData = BuildChartData(fy.OpeningDate, fy.EndDate, active);
        var activity = active
            .OrderByDescending(x => x.ModifiedDate)
            .Take(8)
            .Select(x =>
            {
                var amount = x.IncomeAmount > 0 ? x.IncomeAmount : x.ExpenseAmount;
                var type = x.IncomeAmount > 0 ? "Income" : "Expense";
                return $"{x.Date:dd/MM/yyyy} · {type} · {x.Description} · {amount:C} · {x.CreatedBy}";
            })
            .ToList();

        var expenses = active.Where(x => x.ExpenseAmount > 0).ToList();
        var receiptsAttached = expenses.Count(x => x.HasReceipt);
        var missingReceipts = expenses.Count(x => x.ReceiptRequired);

        var daysUntilEnd = (fy.EndDate.Date - DateTime.Today).Days;
        if (daysUntilEnd < 0) daysUntilEnd = 0;

        var importStatus = await _importService.GetImportStatusAsync();

        return new DashboardSummary
        {
            BankBalance = balance,
            IncomeThisMonth = incomeMonth,
            ExpensesThisMonth = expenseMonth,
            ProfitThisMonth = incomeMonth - expenseMonth,
            CurrentProfitLoss = ytdIncome - ytdExpenses,
            YearToDateIncome = ytdIncome,
            YearToDateExpenses = ytdExpenses,
            YearToDateProfit = ytdIncome - ytdExpenses,
            TransactionCount = active.Count,
            CurrentFinancialYearName = fy.Name,
            FinancialYearEndDate = fy.EndDate,
            DaysUntilYearEnd = daysUntilEnd,
            RecentTransactions = active.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Take(10).ToList(),
            MonthlyChartData = chartData,
            RecentActivity = activity,
            ReceiptsAttached = receiptsAttached,
            MissingReceipts = missingReceipts,
            ImportStatus = importStatus
        };
    }


    private static List<ChartMonthItem> BuildChartData(DateTime start, DateTime end, List<TransactionListItem> active)
    {
        var items = new List<ChartMonthItem>();
        var cursor = new DateTime(start.Year, start.Month, 1);
        var today = DateTime.Today;
        var limit = today < end ? today : end;

        while (cursor <= limit)
        {
            var mStart = cursor;
            var mEnd = cursor.AddMonths(1).AddDays(-1);
            var monthTxns = active.Where(x => x.Date >= mStart && x.Date <= mEnd).ToList();
            items.Add(new ChartMonthItem
            {
                Label = cursor.ToString("MMM"),
                Income = monthTxns.Sum(x => x.IncomeAmount),
                Expenses = monthTxns.Sum(x => x.ExpenseAmount)
            });
            cursor = cursor.AddMonths(1);
        }

        var maxValue = items.SelectMany(x => new[] { x.Income, x.Expenses, Math.Abs(x.Profit) }).DefaultIfEmpty(0).Max();
        if (maxValue <= 0) maxValue = 1;

        foreach (var item in items)
        {
            item.IncomeBarHeight = (double)(item.Income / maxValue) * MaxBarHeight;
            item.ExpenseBarHeight = (double)(item.Expenses / maxValue) * MaxBarHeight;
            item.ProfitBarHeight = (double)(Math.Abs(item.Profit) / maxValue) * MaxBarHeight;
        }

        return items;
    }
}
