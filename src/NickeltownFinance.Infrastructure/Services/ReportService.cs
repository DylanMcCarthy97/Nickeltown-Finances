using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Reports.Exporters;

namespace NickeltownFinance.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly IFinancialYearService _financialYearService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsService _settingsService;
    private readonly ISessionService _sessionService;
    private readonly IUserRepository _userRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ITreasurerMonthSnapshotRepository _snapshotRepository;

    public ReportService(
        IFinancialYearRepository financialYearRepository,
        IFinancialYearService financialYearService,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ISettingsService settingsService,
        ISessionService sessionService,
        IUserRepository userRepository,
        IAttachmentRepository attachmentRepository,
        ITreasurerMonthSnapshotRepository snapshotRepository)
    {
        _financialYearRepository = financialYearRepository;
        _financialYearService = financialYearService;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _settingsService = settingsService;
        _sessionService = sessionService;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _snapshotRepository = snapshotRepository;
    }

    public Task<MonthlyReportData> BuildMonthlyReportAsync(ObjectId financialYearId, int year, int month, string notes = "")
    {
        var fy = _financialYearRepository.GetById(financialYearId)
                 ?? throw new InvalidOperationException("Financial year not found.");

        _financialYearService.RecalculateAllOpeningBalances();

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var opening = CalculateBalanceAsOf(monthStart.AddDays(-1));
        var monthTxns = _transactionRepository.GetByDateRange(monthStart, monthEnd)
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.Description)
            .ToList();

        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);

        var incomeByCat = monthTxns.Where(t => t.IncomeAmount > 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryTotal
            {
                CategoryName = categories.GetValueOrDefault(g.Key)?.Name ?? "Unknown",
                Amount = g.Sum(x => x.IncomeAmount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var expenseByCat = monthTxns.Where(t => t.ExpenseAmount > 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryTotal
            {
                CategoryName = categories.GetValueOrDefault(g.Key)?.Name ?? "Unknown",
                Amount = g.Sum(x => x.ExpenseAmount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var transactions = monthTxns.Select(t => new ReportTransactionLine
        {
            Date = t.Date,
            Description = string.IsNullOrWhiteSpace(t.Description) ? "—" : t.Description.Trim(),
            CategoryName = categories.GetValueOrDefault(t.CategoryId)?.Name ?? "Uncategorised",
            MoneyIn = t.IncomeAmount,
            MoneyOut = t.ExpenseAmount
        }).ToList();

        var totalIncome = incomeByCat.Sum(x => x.Amount);
        var totalExpenses = expenseByCat.Sum(x => x.Amount);
        var (preparedBy, role, signaturePath) = GetPreparer();

        var data = new MonthlyReportData
        {
            ClubName = _settingsService.ClubName,
            LogoPath = ResolveLogoPath(),
            FinancialYearName = fy.Name,
            MonthName = monthStart.ToString("MMMM"),
            Month = month,
            Year = year,
            OpeningBalance = opening,
            IncomeByCategory = incomeByCat,
            TotalIncome = totalIncome,
            ExpensesByCategory = expenseByCat,
            TotalExpenses = totalExpenses,
            MonthlyProfit = totalIncome - totalExpenses,
            Transactions = transactions,
            Notes = notes,
            PreparedBy = preparedBy,
            PreparedByRole = role,
            PrintedAt = DateTime.Now,
            SignatureImagePath = signaturePath
        };
        ApplyClosingBalance(data, monthEnd);
        ApplyHoldings(data, year, month);

        return Task.FromResult(data);
    }

    public Task<AgmReportData> BuildAgmReportAsync(ObjectId financialYearId)
    {
        var fy = _financialYearRepository.GetById(financialYearId)
                 ?? throw new InvalidOperationException("Financial year not found.");

        _financialYearService.RecalculateAllOpeningBalances();
        fy = _financialYearRepository.GetById(financialYearId) ?? fy;

        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var txns = _transactionRepository.GetByFinancialYear(financialYearId)
            .Where(t => !t.IsDeleted)
            .ToList();

        var monthlyData = new List<MonthlyBreakdown>();
        var cursor = new DateTime(fy.OpeningDate.Year, fy.OpeningDate.Month, 1);

        while (cursor <= fy.EndDate)
        {
            var mStart = cursor;
            var mEnd = cursor.AddMonths(1).AddDays(-1);
            if (mEnd > fy.EndDate) mEnd = fy.EndDate;

            var monthTxns = txns.Where(t => t.Date >= mStart && t.Date <= mEnd).ToList();
            monthlyData.Add(new MonthlyBreakdown
            {
                MonthName = cursor.ToString("MMM yyyy"),
                Month = cursor.Month,
                Income = monthTxns.Sum(t => t.IncomeAmount),
                Expenses = monthTxns.Sum(t => t.ExpenseAmount)
            });

            cursor = cursor.AddMonths(1);
        }

        var incomeByCat = txns.Where(t => t.IncomeAmount > 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryTotal
            {
                CategoryName = categories.GetValueOrDefault(g.Key)?.Name ?? "Unknown",
                Amount = g.Sum(x => x.IncomeAmount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var expenseByCat = txns.Where(t => t.ExpenseAmount > 0)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryTotal
            {
                CategoryName = categories.GetValueOrDefault(g.Key)?.Name ?? "Unknown",
                Amount = g.Sum(x => x.ExpenseAmount)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        var totalIncome = incomeByCat.Sum(x => x.Amount);
        var totalExpenses = expenseByCat.Sum(x => x.Amount);
        var opening = CalculateBalanceAsOf(fy.OpeningDate.AddDays(-1));
        var (preparedBy, role, signaturePath) = GetPreparer();

        var data = new AgmReportData
        {
            ClubName = _settingsService.ClubName,
            LogoPath = ResolveLogoPath(),
            FinancialYearName = fy.Name,
            OpeningBalance = opening,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            AnnualProfit = totalIncome - totalExpenses,
            MonthlyData = monthlyData,
            IncomeByCategory = incomeByCat,
            ExpensesByCategory = expenseByCat,
            FinancialYearEndDate = fy.EndDate,
            PreparedBy = preparedBy,
            PreparedByRole = role,
            PrintedAt = DateTime.Now,
            SignatureImagePath = signaturePath
        };
        ApplyClosingBalance(data);
        var holdingsMonth = data.ClosingBalanceAsOf;
        ApplyHoldings(data, holdingsMonth.Year, holdingsMonth.Month);

        return Task.FromResult(data);
    }

    public Task<string> ExportMonthlyPdfAsync(MonthlyReportData data, string outputPath) =>
        Task.FromResult(MonthlyReportExporter.ExportPdf(data, outputPath));

    public Task<string> ExportMonthlyExcelAsync(MonthlyReportData data, string outputPath) =>
        Task.FromResult(MonthlyReportExporter.ExportExcel(data, outputPath));

    public Task<string> ExportAgmPdfAsync(AgmReportData data, string outputPath) =>
        Task.FromResult(AgmReportExporter.ExportPdf(data, outputPath));

    public Task<string> ExportAgmExcelAsync(AgmReportData data, string outputPath) =>
        Task.FromResult(AgmReportExporter.ExportExcel(data, outputPath));

    public Task<string> ExportCategorySummaryExcelAsync(ObjectId financialYearId, string outputPath)
    {
        var fy = _financialYearRepository.GetById(financialYearId)
            ?? throw new InvalidOperationException("Financial year not found.");
        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var txns = _transactionRepository.GetByFinancialYear(financialYearId).Where(t => !t.IsDeleted).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Category Summary");
        sheet.Cell(1, 1).Value = $"{_settingsService.ClubName} — Category Summary — FY {fy.Name}";
        sheet.Cell(1, 1).Style.Font.Bold = true;

        sheet.Cell(3, 1).Value = "Category";
        sheet.Cell(3, 2).Value = "Income";
        sheet.Cell(3, 3).Value = "Expenses";
        sheet.Cell(3, 4).Value = "Net";
        sheet.Row(3).Style.Font.Bold = true;

        var row = 4;
        foreach (var group in txns.GroupBy(t => t.CategoryId).OrderBy(g => categories.GetValueOrDefault(g.Key)?.Name))
        {
            var name = categories.GetValueOrDefault(group.Key)?.Name ?? "Uncategorised";
            var income = group.Sum(t => t.IncomeAmount);
            var expense = group.Sum(t => t.ExpenseAmount);
            sheet.Cell(row, 1).Value = name;
            sheet.Cell(row, 2).Value = income;
            sheet.Cell(row, 3).Value = expense;
            sheet.Cell(row, 4).Value = income - expense;
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
        return Task.FromResult(outputPath);
    }

    public Task<string> ExportGstSummaryExcelAsync(ObjectId financialYearId, string outputPath)
    {
        var fy = _financialYearRepository.GetById(financialYearId)
            ?? throw new InvalidOperationException("Financial year not found.");
        var txns = _transactionRepository.GetByFinancialYear(financialYearId).Where(t => !t.IsDeleted).ToList();
        var attachments = _attachmentRepository.GetByTransactions(txns.Select(t => t.Id))
            .Where(a => a.Kind == AttachmentKind.Receipt)
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("GST Summary");
        sheet.Cell(1, 1).Value = $"{_settingsService.ClubName} — GST Summary — FY {fy.Name}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(3, 1).Value = "Receipt GST (from OCR)";
        sheet.Cell(3, 2).Value = attachments.Where(a => a.OcrGst.HasValue).Sum(a => a.OcrGst!.Value);
        sheet.Cell(4, 1).Value = "Receipts with GST data";
        sheet.Cell(4, 2).Value = attachments.Count(a => a.OcrGst.HasValue);
        sheet.Cell(5, 1).Value = "Total expense transactions";
        sheet.Cell(5, 2).Value = txns.Count(t => t.ExpenseAmount > 0);
        sheet.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
        return Task.FromResult(outputPath);
    }

    public Task<string> ExportReceiptAuditExcelAsync(ObjectId financialYearId, string outputPath)
    {
        var fy = _financialYearRepository.GetById(financialYearId)
            ?? throw new InvalidOperationException("Financial year not found.");
        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var txns = _transactionRepository.GetByFinancialYear(financialYearId)
            .Where(t => !t.IsDeleted && t.ExpenseAmount > 0)
            .OrderBy(t => t.Date)
            .ToList();
        var receiptTxnIds = _attachmentRepository.GetTransactionIdsWithAttachments(AttachmentKind.Receipt);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Receipt Audit");
        sheet.Cell(1, 1).Value = $"{_settingsService.ClubName} — Receipt Audit — FY {fy.Name}";
        sheet.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "Date", "Description", "Category", "Amount", "Receipt Status" };
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(3, i + 1).Value = headers[i];
        sheet.Row(3).Style.Font.Bold = true;

        var row = 4;
        foreach (var txn in txns)
        {
            sheet.Cell(row, 1).Value = txn.Date;
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 2).Value = txn.Description;
            sheet.Cell(row, 3).Value = categories.GetValueOrDefault(txn.CategoryId)?.Name ?? "Uncategorised";
            sheet.Cell(row, 4).Value = txn.ExpenseAmount;
            sheet.Cell(row, 5).Value = receiptTxnIds.Contains(txn.Id) ? "Attached" : "Missing";
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
        return Task.FromResult(outputPath);
    }

    public void ApplyPrintDate(MonthlyReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.PrintedAt = DateTime.Now;
        var monthEnd = new DateTime(data.Year, data.Month, 1).AddMonths(1).AddDays(-1);
        ApplyClosingBalance(data, monthEnd);
        ApplyHoldings(data, data.ClosingBalanceAsOf.Year, data.ClosingBalanceAsOf.Month);
    }

    public void ApplyPrintDate(AgmReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.PrintedAt = DateTime.Now;
        ApplyClosingBalance(data);
        ApplyHoldings(data, data.ClosingBalanceAsOf.Year, data.ClosingBalanceAsOf.Month);
    }

    private void ApplyHoldings(MonthlyReportData data, int year, int month)
    {
        var (cash, bonds, paypal) = ResolveHoldings(year, month);
        data.CashOnHand = cash;
        data.ShireBonds = bonds;
        data.PayPalBalance = paypal;
    }

    private void ApplyHoldings(AgmReportData data, int year, int month)
    {
        var (cash, bonds, paypal) = ResolveHoldings(year, month);
        data.CashOnHand = cash;
        data.ShireBonds = bonds;
        data.PayPalBalance = paypal;
    }

    private (decimal CashOnHand, decimal ShireBonds, decimal PayPalBalance) ResolveHoldings(int year, int month)
    {
        var snapshot = _snapshotRepository.GetByYearMonth(year, month);
        return (
            snapshot?.CashOnHand ?? _settingsService.DefaultCashOnHand,
            snapshot?.ShireBonds ?? _settingsService.DefaultShireBonds,
            snapshot?.PayPalBalance ?? _settingsService.DefaultPayPalBalance);
    }

    private void ApplyClosingBalance(MonthlyReportData data, DateTime monthEnd)
    {
        data.ClosingBalanceAsOf = ResolveClosingBalanceAsOf(data.PrintedAt, monthEnd);
        data.ClosingBalance = CalculateBalanceAsOf(data.ClosingBalanceAsOf);
    }

    private void ApplyClosingBalance(AgmReportData data)
    {
        data.ClosingBalanceAsOf = ResolveClosingBalanceAsOf(data.PrintedAt, data.FinancialYearEndDate);
        data.ClosingBalance = CalculateBalanceAsOf(data.ClosingBalanceAsOf);
    }

    private static DateTime ResolveClosingBalanceAsOf(DateTime printedAt, DateTime periodEnd)
    {
        var printDate = printedAt.Date;
        var endDate = periodEnd.Date;
        return printDate <= endDate ? printDate : endDate;
    }

    /// <summary>
    /// Bank balance at the end of <paramref name="asOfDate"/>, using the club tracking start.
    /// Tracking balance is end-of-day on the starting date (includes that day's transactions).
    /// </summary>
    private decimal CalculateBalanceAsOf(DateTime asOfDate)
    {
        var (trackingDate, trackingBalance) = _financialYearService.GetTrackingStart();
        var asOf = asOfDate.Date;

        if (asOf == trackingDate)
            return trackingBalance;

        decimal net = 0;
        foreach (var txn in _transactionRepository.GetAll())
        {
            if (txn.IsDeleted) continue;
            var day = txn.Date.Date;
            var amount = txn.IncomeAmount - txn.ExpenseAmount;

            if (asOf < trackingDate)
            {
                // Working backwards: remove activity from after asOf through the starting date.
                if (day > asOf && day <= trackingDate)
                    net += amount;
            }
            else if (day > trackingDate && day <= asOf)
            {
                // After tracking start: only activity strictly after the starting date.
                net += amount;
            }
        }

        return asOf < trackingDate
            ? trackingBalance - net
            : trackingBalance + net;
    }

    private (string Name, string Role, string? SignaturePath) GetPreparer()
    {
        var sessionUser = _sessionService.CurrentUser;
        if (sessionUser is null)
            return ("Treasurer", "Treasurer", null);

        // Reload so the latest full name and signature are used on export.
        var user = _userRepository.GetById(sessionUser.Id) ?? sessionUser;

        // Reports always sign with the user's full name — never the login username.
        var fullName = !string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.DisplayName.Trim()
            : "Treasurer";

        var role = user.Role switch
        {
            UserRole.Administrator => "Administrator",
            UserRole.Treasurer => "Treasurer",
            UserRole.Committee => "Committee",
            UserRole.ReadOnly => "Read only",
            _ => user.Role.ToString()
        };

        string? signature = null;
        if (!string.IsNullOrWhiteSpace(user.SignatureImagePath) && File.Exists(user.SignatureImagePath))
            signature = user.SignatureImagePath;

        return (fullName, role, signature);
    }

    private string? ResolveLogoPath()
    {
        var path = _settingsService.ClubLogoPath;
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return File.Exists(path) ? path : null;
    }
}
