using ClosedXML.Excel;
using LiteDB;
using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Helpers;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using TransactionModel = NickeltownFinance.Core.Models.Transaction;


namespace NickeltownFinance.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly IFinancialYearService _financialYearService;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ISessionService _sessionService;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IFinancialYearRepository financialYearRepository,
        IFinancialYearService financialYearService,
        IAttachmentRepository attachmentRepository,
        ISessionService sessionService)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _financialYearRepository = financialYearRepository;
        _financialYearService = financialYearService;
        _attachmentRepository = attachmentRepository;
        _sessionService = sessionService;
    }

    public Task<IReadOnlyList<TransactionListItem>> GetLedgerAsync(
        ObjectId financialYearId,
        string? search = null,
        ObjectId? categoryFilter = null,
        bool includeDeleted = false,
        TransactionSearchFilter? filter = null)
    {
        search ??= filter?.Search;
        categoryFilter ??= filter?.CategoryId;
        includeDeleted = includeDeleted || (filter?.IncludeDeleted ?? false);

        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var fy = _financialYearRepository.GetById(financialYearId);
        var running = fy?.OpeningBalance ?? 0;

        var allTransactions = _transactionRepository.GetByFinancialYear(financialYearId, true).ToList();
        var balanceById = new Dictionary<ObjectId, decimal>();
        var attachmentsByTxn = _attachmentRepository
            .GetByTransactions(allTransactions.Select(t => t.Id))
            .GroupBy(a => a.TransactionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        HashSet<ObjectId>? filenameMatchIds = null;
        HashSet<ObjectId>? ocrMatchIds = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filenameMatchIds = _attachmentRepository.SearchByFileName(search)
                .Select(a => a.TransactionId)
                .ToHashSet();
            ocrMatchIds = _attachmentRepository.SearchByOcrText(search)
                .Select(a => a.TransactionId)
                .ToHashSet();
        }

        // Calculate full account balance for all transactions
        foreach (var txn in allTransactions)
        {
            if (!txn.IsDeleted)
                running += txn.IncomeAmount - txn.ExpenseAmount;
            balanceById[txn.Id] = running;
        }

        // Check if filters are active (excluding includeDeleted flag)
        var hasActiveFilters = !string.IsNullOrWhiteSpace(search) ||
                              categoryFilter is not null && categoryFilter != ObjectId.Empty ||
                              filter?.FromDate is not null ||
                              filter?.ToDate is not null ||
                              filter?.IsIncome is not null ||
                              filter?.HasReceipt is not null ||
                              filter?.ReceiptType is not null;

        var items = new List<TransactionListItem>();
        var filteredBalance = fy?.OpeningBalance ?? 0; // For filtered cumulative balance

        foreach (var txn in allTransactions)
        {
            if (txn.IsDeleted && !includeDeleted)
                continue;

            if (filter?.FromDate is { } from && txn.Date.Date < from.Date)
                continue;
            if (filter?.ToDate is { } to && txn.Date.Date > to.Date)
                continue;

            if (filter?.IsIncome == true && txn.IncomeAmount <= 0)
                continue;
            if (filter?.IsIncome == false && txn.ExpenseAmount <= 0)
                continue;

            attachmentsByTxn.TryGetValue(txn.Id, out var attachments);
            attachments ??= [];
            var hasReceipt = attachments.Any(a => a.Kind == AttachmentKind.Receipt);
            var attachmentCount = attachments.Count;

            // HasReceipt=true: any transaction with a receipt
            // HasReceipt=false: expenses missing receipts ("Receipt Required")
            if (filter?.HasReceipt == true && !hasReceipt)
                continue;
            if (filter?.HasReceipt == false && !(txn.ExpenseAmount > 0 && !hasReceipt))
                continue;

            if (filter?.ReceiptType is not null && attachments.All(a => a.Kind != filter.ReceiptType))
                continue;

            categories.TryGetValue(txn.CategoryId, out var cat);
            
            // Determine which balance to show
            decimal displayBalance;
            if (hasActiveFilters)
            {
                // Show cumulative balance of filtered transactions only
                if (!txn.IsDeleted)
                    filteredBalance += txn.IncomeAmount - txn.ExpenseAmount;
                displayBalance = filteredBalance;
            }
            else
            {
                // Show actual account balance (no filters, just ledger view)
                displayBalance = balanceById.GetValueOrDefault(txn.Id);
            }
            
            var item = MapToListItem(txn, categories, displayBalance, attachments, hasReceipt, attachmentCount);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var amountMatch = false;
                if (decimal.TryParse(s.Replace("$", "").Replace(",", ""), out var searchAmount))
                {
                    amountMatch = txn.IncomeAmount == searchAmount ||
                                  txn.ExpenseAmount == searchAmount ||
                                  Math.Abs(txn.IncomeAmount - searchAmount) < 0.001m ||
                                  Math.Abs(txn.ExpenseAmount - searchAmount) < 0.001m;
                }

                var dateMatch =
                    txn.Date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
                        .Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    txn.Date.ToString("d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture)
                        .Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    txn.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                        .Contains(s, StringComparison.OrdinalIgnoreCase);

                var textMatch =
                    item.Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    item.CategoryName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (item.Notes?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.Reference?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    item.PaymentMethod.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    item.CreatedBy.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    amountMatch ||
                    dateMatch ||
                    (filenameMatchIds is not null && filenameMatchIds.Contains(txn.Id)) ||
                    (ocrMatchIds is not null && ocrMatchIds.Contains(txn.Id));
                if (!textMatch)
                {
                    // If this transaction doesn't match the search but we're using filtered balance,
                    // we need to revert the balance update we just made
                    if (hasActiveFilters && !txn.IsDeleted)
                        filteredBalance -= txn.IncomeAmount - txn.ExpenseAmount;
                    continue;
                }
            }

            if (categoryFilter is not null && categoryFilter != ObjectId.Empty)
            {
                var matchesCategory = txn.CategoryId == categoryFilter
                    || TransactionCategoryHelper.GetIncomeCategoryAmounts(txn).Any(a => a.CategoryId == categoryFilter);
                if (!matchesCategory)
                {
                    if (hasActiveFilters && !txn.IsDeleted)
                        filteredBalance -= txn.IncomeAmount - txn.ExpenseAmount;
                    continue;
                }
            }

            items.Add(item);
        }


        return Task.FromResult<IReadOnlyList<TransactionListItem>>(items);
    }

    public Task<TransactionListItem?> GetByIdAsync(ObjectId id)
    {
        var txn = _transactionRepository.GetById(id);
        if (txn is null) return Task.FromResult<TransactionListItem?>(null);

        var categories = _categoryRepository.GetAll().ToDictionary(c => c.Id);
        var balance = CalculateRunningBalance(txn.FinancialYearId, txn.Id);
        var attachments = _attachmentRepository.GetByTransaction(txn.Id).ToList();
        return Task.FromResult<TransactionListItem?>(MapToListItem(
            txn, categories, balance,
            attachments,
            attachments.Any(a => a.Kind == AttachmentKind.Receipt),
            attachments.Count));
    }

    public Task SaveAsync(TransactionModel transaction, bool isIncome)
    {
        if (isIncome)
        {
            transaction.ExpenseAmount = 0;
            if (transaction.IncomeAmount <= 0)
                throw new InvalidOperationException("Income amount must be greater than zero.");
        }
        else
        {
            transaction.IncomeAmount = 0;
            if (transaction.ExpenseAmount <= 0)
                throw new InvalidOperationException("Expense amount must be greater than zero.");
        }

        var fy = _financialYearService.EnsureYearForDate(transaction.Date);
        if (fy.IsLocked)
            throw new InvalidOperationException("This financial year is locked and cannot accept changes.");

        var user = _sessionService.CurrentUser;
        var userName = user?.DisplayName ?? "System";
        var userId = user?.Id ?? ObjectId.Empty;
        var now = DateTime.UtcNow;

        transaction.FinancialYearId = fy.Id;
        transaction.IsDeleted = false;
        transaction.DeletedAt = null;
        transaction.ModifiedDate = now;
        transaction.ModifiedByUserId = userId;
        transaction.ModifiedByName = userName;

        if (transaction.Id == ObjectId.Empty)
        {
            transaction.CreatedDate = now;
            transaction.CreatedByUserId = userId;
            transaction.CreatedByName = userName;
            _transactionRepository.Insert(transaction);
        }
        else
        {
            var existing = _transactionRepository.GetById(transaction.Id);
            if (existing is not null)
            {
                transaction.CreatedDate = existing.CreatedDate;
                transaction.CreatedByUserId = existing.CreatedByUserId;
                transaction.CreatedByName = existing.CreatedByName;
            }

            _transactionRepository.Update(transaction);
        }

        _financialYearService.RecalculateAllOpeningBalances();
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(ObjectId id)
    {
        var txn = _transactionRepository.GetById(id);
        if (txn is null) return Task.CompletedTask;

        if (_financialYearService.IsLocked(txn.FinancialYearId))
            throw new InvalidOperationException("This financial year is locked and cannot accept changes.");

        var user = _sessionService.CurrentUser;
        txn.IsDeleted = true;
        txn.DeletedAt = DateTime.UtcNow;
        txn.ModifiedDate = DateTime.UtcNow;
        txn.ModifiedByUserId = user?.Id ?? ObjectId.Empty;
        txn.ModifiedByName = user?.DisplayName ?? "System";
        _transactionRepository.Update(txn);
        _financialYearService.RecalculateAllOpeningBalances();
        return Task.CompletedTask;
    }

    public Task RestoreAsync(ObjectId id)
    {
        var txn = _transactionRepository.GetById(id);
        if (txn is null) return Task.CompletedTask;

        if (_financialYearService.IsLocked(txn.FinancialYearId))
            throw new InvalidOperationException("This financial year is locked and cannot accept changes.");

        var user = _sessionService.CurrentUser;
        txn.IsDeleted = false;
        txn.DeletedAt = null;
        txn.ModifiedDate = DateTime.UtcNow;
        txn.ModifiedByUserId = user?.Id ?? ObjectId.Empty;
        txn.ModifiedByName = user?.DisplayName ?? "System";
        _transactionRepository.Update(txn);
        _financialYearService.RecalculateAllOpeningBalances();
        return Task.CompletedTask;
    }

    public Task DuplicateAsync(ObjectId id)
    {
        var txn = _transactionRepository.GetById(id);
        if (txn is null) return Task.CompletedTask;

        if (_financialYearService.IsLocked(txn.FinancialYearId))
            throw new InvalidOperationException("This financial year is locked and cannot accept changes.");

        var user = _sessionService.CurrentUser;
        var now = DateTime.UtcNow;
        var copy = new TransactionModel
        {
            Date = txn.Date,
            Description = txn.Description + " (Copy)",
            CategoryId = txn.CategoryId,
            CategoryAllocations = txn.CategoryAllocations?
                .Select(a => new CategoryAllocation { CategoryId = a.CategoryId, Amount = a.Amount })
                .ToList() ?? [],
            IncomeAmount = txn.IncomeAmount,
            ExpenseAmount = txn.ExpenseAmount,
            PaymentMethod = txn.PaymentMethod,
            Reference = txn.Reference,
            Notes = txn.Notes,
            IsSquareDeposit = txn.IsSquareDeposit,
            FinancialYearId = txn.FinancialYearId,
            CreatedByUserId = user?.Id ?? ObjectId.Empty,
            CreatedByName = user?.DisplayName ?? "System",
            ModifiedByUserId = user?.Id ?? ObjectId.Empty,
            ModifiedByName = user?.DisplayName ?? "System",
            CreatedDate = now,
            ModifiedDate = now
        };

        _transactionRepository.Insert(copy);
        _financialYearService.RecalculateAllOpeningBalances();
        return Task.CompletedTask;
    }

    public async Task<string> ExportLedgerExcelAsync(
        ObjectId financialYearId,
        string outputPath,
        string? search = null,
        ObjectId? categoryFilter = null)
    {
        var items = await GetLedgerAsync(financialYearId, search, categoryFilter);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Transactions");

        var headers = new[]
        {
            "Date", "Description", "Category", "Income", "Expense", "Payment Method",
            "Reference", "Notes", "Running Balance", "Created By", "Last Modified"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Date;
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.CategoryName;
            sheet.Cell(row, 4).Value = item.IncomeAmount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Cell(row, 5).Value = item.ExpenseAmount;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Cell(row, 6).Value = item.PaymentMethod;
            sheet.Cell(row, 7).Value = item.Reference;
            sheet.Cell(row, 8).Value = item.Notes;
            sheet.Cell(row, 9).Value = item.RunningBalance;
            sheet.Cell(row, 9).Style.NumberFormat.Format = "$#,##0.00";
            sheet.Cell(row, 10).Value = item.CreatedBy;
            sheet.Cell(row, 11).Value = item.LastModified;
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
        return outputPath;
    }

    public decimal CalculateRunningBalance(ObjectId financialYearId, DateTime asOfDate, ObjectId? upToTransactionId = null)
    {
        if (upToTransactionId is { } txnId && txnId != ObjectId.Empty)
            return CalculateRunningBalance(financialYearId, txnId);

        var fy = _financialYearRepository.GetById(financialYearId);
        var balance = fy?.OpeningBalance ?? 0;

        foreach (var txn in _transactionRepository.GetByFinancialYear(financialYearId))
        {
            if (txn.IsDeleted) continue;
            if (txn.Date > asOfDate) break;
            balance += txn.IncomeAmount - txn.ExpenseAmount;
        }

        return balance;
    }

    private decimal CalculateRunningBalance(ObjectId financialYearId, ObjectId transactionId)
    {
        var fy = _financialYearRepository.GetById(financialYearId);
        var balance = fy?.OpeningBalance ?? 0;

        foreach (var txn in _transactionRepository.GetByFinancialYear(financialYearId))
        {
            if (txn.IsDeleted) continue;
            balance += txn.IncomeAmount - txn.ExpenseAmount;
            if (txn.Id == transactionId) break;
        }

        return balance;
    }

    private static TransactionListItem MapToListItem(
        TransactionModel txn,
        IReadOnlyDictionary<ObjectId, Category> categories,
        decimal balance,
        IReadOnlyList<Core.Models.Attachment> attachments,
        bool hasReceipt = false,
        int attachmentCount = 0)
    {
        var thumb = attachments
            .Select(a => ResolveThumbnail(a))
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

        categories.TryGetValue(txn.CategoryId, out var cat);
        var categoryName = txn.IncomeAmount > 0
            ? TransactionCategoryHelper.FormatCategoryDisplay(txn, categories)
            : cat?.Name ?? "Unknown";

        return new()
        {
            Id = txn.Id,
            Date = txn.Date,
            Description = txn.Description,
            CategoryName = categoryName,
            CategoryColour = cat?.Colour ?? "#1565C0",
            CategoryId = txn.CategoryId,
            IncomeAmount = txn.IncomeAmount,
            ExpenseAmount = txn.ExpenseAmount,
            PaymentMethod = txn.PaymentMethod.ToString(),
            Reference = txn.Reference,
            Notes = txn.Notes,
            RunningBalance = balance,
            CreatedBy = txn.CreatedByName,
            LastModified = string.IsNullOrWhiteSpace(txn.ModifiedByName)
                ? txn.ModifiedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : $"{txn.ModifiedByName} · {txn.ModifiedDate.ToLocalTime():dd/MM/yyyy HH:mm}",
            ModifiedDate = txn.ModifiedDate,
            IsDeleted = txn.IsDeleted,
            AttachmentCount = attachmentCount,
            HasReceipt = hasReceipt,
            ThumbnailPath = thumb,
            IsSquareDeposit = txn.IsSquareDeposit,
            HasSquareDepositDetail = txn.IsSquareDeposit && txn.SquareDepositId is not null,
            SquareDepositId = txn.SquareDepositId
        };
    }

    private static string? ResolveThumbnail(Core.Models.Attachment attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.ThumbnailRelativePath))
            return AppPaths.ResolvePath(attachment.ThumbnailRelativePath);
        var path = AppPaths.ResolvePath(attachment.RelativePath);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".tif" or ".tiff" ? path : null;
    }
}

