using System.Diagnostics;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Import;
using TransactionModel = NickeltownFinance.Core.Models.Transaction;

namespace NickeltownFinance.Infrastructure.Services;

public class StatementImportService : IStatementImportService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFinancialYearService _financialYearService;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImportBatchRepository _importBatchRepository;
    private readonly ICategorisationService _categorisationService;
    private readonly IReconciliationService _reconciliationService;
    private readonly ISquareDepositRepository _squareDepositRepository;
    private readonly ISessionService _sessionService;

    public StatementImportService(
        ITransactionRepository transactionRepository,
        IFinancialYearService financialYearService,
        ICategoryRepository categoryRepository,
        IImportBatchRepository importBatchRepository,
        ICategorisationService categorisationService,
        IReconciliationService reconciliationService,
        ISquareDepositRepository squareDepositRepository,
        ISessionService sessionService)
    {
        _transactionRepository = transactionRepository;
        _financialYearService = financialYearService;
        _categoryRepository = categoryRepository;
        _importBatchRepository = importBatchRepository;
        _categorisationService = categorisationService;
        _reconciliationService = reconciliationService;
        _squareDepositRepository = squareDepositRepository;
        _sessionService = sessionService;
    }

    public async Task<ImportAnalyseResult> AnalyseAsync(string filePath, AnzColumnMapping? mapping = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Statement file not found.", filePath);

        StatementParseResult parsed;
        try
        {
            parsed = ParseFile(filePath, mapping);
        }
        catch (AnzCsvParseException ex)
        {
            return new ImportAnalyseResult { Failure = ex.Failure };
        }

        var previewRows = new List<ImportPreviewRow>();

        var minDate = parsed.Rows.Count > 0 ? parsed.Rows.Min(r => r.Date).AddDays(-1) : DateTime.Today;
        var maxDate = parsed.Rows.Count > 0 ? parsed.Rows.Max(r => r.Date).AddDays(1) : DateTime.Today;
        var existing = _transactionRepository.GetByDateRange(minDate, maxDate).ToList();
        var byFingerprint = existing
            .Where(t => !string.IsNullOrWhiteSpace(t.ImportFingerprint))
            .GroupBy(t => t.ImportFingerprint)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var row in parsed.Rows)
        {
            var isIncome = row.Credit > 0;
            var suggestion = await _categorisationService.SuggestDetailedAsync(row.Description, isIncome);

            var isSquareDeposit = suggestion.IsSquareDeposit ||
                                  ReconciliationService.IsSquareTransferDescription(row.Description);
            var categoryName = isSquareDeposit
                ? (string.IsNullOrWhiteSpace(suggestion.CategoryName) ? "Square Deposit" : suggestion.CategoryName)
                : suggestion.CategoryName;
            var hasCategory = suggestion.CategoryId is not null && suggestion.CategoryId != ObjectId.Empty;

            var preview = new ImportPreviewRow
            {
                Date = row.Date,
                Description = row.Description,
                Debit = row.Debit,
                Credit = row.Credit,
                Balance = row.Balance,
                Reference = row.Reference,
                Fingerprint = row.Fingerprint,
                SuggestedCategoryId = suggestion.CategoryId,
                SuggestedCategoryName = hasCategory ? categoryName : string.Empty,
                IsSquareDeposit = isSquareDeposit,
                Status = hasCategory || isSquareDeposit ? ImportRowStatus.New : ImportRowStatus.NeedsReview,
                IsSelected = true
            };

            DetectDuplicate(preview, row, byFingerprint, existing);
            previewRows.Add(preview);
        }

        return new ImportAnalyseResult
        {
            Preview = new ImportPreview
            {
                FileName = parsed.FileName,
                Format = parsed.Format,
                BankName = parsed.BankName,
                Rows = previewRows,
                Warnings = parsed.Warnings
            }
        };
    }


    public async Task<ImportResult> CommitAsync(ImportCommitRequest request)
    {
        var sw = Stopwatch.StartNew();
        var imported = 0;
        var skipped = 0;
        var duplicates = 0;
        var errors = 0;
        var errorMessages = new List<string>();

        var user = _sessionService.CurrentUser;
        var userId = user?.Id ?? ObjectId.Empty;
        var userName = user?.DisplayName ?? "System";
        var now = DateTime.UtcNow;

        var batch = new ImportBatch
        {
            ImportedAt = now,
            FileName = request.FileName,
            SourceFormat = request.Format,
            SourceType = ImportSourceType.AnzBank,
            BankName = request.BankName,
            UserId = userId,
            UserName = userName
        };
        _importBatchRepository.Insert(batch);

        var toInsert = new List<TransactionModel>();

        foreach (var row in request.Rows)
        {
            if (!row.IsSelected || row.Status == ImportRowStatus.Ignored)
            {
                skipped++;
                continue;
            }

            if (row.Status == ImportRowStatus.Duplicate && !row.IsSelected)
            {
                duplicates++;
                skipped++;
                continue;
            }

            try
            {
                if (row.Debit <= 0 && row.Credit <= 0)
                {
                    errors++;
                    errorMessages.Add($"Invalid amount for '{row.Description}'.");
                    continue;
                }

                var isIncome = row.Credit > 0;
                var amount = isIncome ? row.Credit : row.Debit;
                var fy = _financialYearService.EnsureYearForDate(row.Date);

                if (fy.IsLocked)
                    throw new InvalidOperationException($"Financial year {fy.Name} is locked.");

                var isSquareDeposit = row.IsSquareDeposit ||
                                      ReconciliationService.IsSquareTransferDescription(row.Description);

                var categoryId = row.SuggestedCategoryId ?? ObjectId.Empty;
                if (categoryId == ObjectId.Empty)
                {
                    var fallbackName = isSquareDeposit ? "Square Deposits" : null;
                    var fallback = fallbackName is not null
                        ? _categoryRepository.GetByType(CategoryType.Income)
                            .FirstOrDefault(c => c.IsActive &&
                                                 string.Equals(c.Name, fallbackName, StringComparison.OrdinalIgnoreCase))
                        : null;
                    fallback ??= _categoryRepository.GetByType(isIncome ? CategoryType.Income : CategoryType.Expense)
                        .FirstOrDefault(c => c.IsActive);
                    categoryId = fallback?.Id ?? ObjectId.Empty;
                }

                if (categoryId == ObjectId.Empty)
                {
                    errors++;
                    errorMessages.Add($"No category available for '{row.Description}'.");
                    continue;
                }

                var txn = new TransactionModel
                {
                    Id = ObjectId.NewObjectId(),
                    Date = row.Date.Date,
                    Description = row.Description.Trim(),
                    Notes = row.Notes.Trim(),
                    Reference = row.Reference.Trim(),
                    CategoryId = categoryId,
                    IncomeAmount = isIncome ? amount : 0,
                    ExpenseAmount = isIncome ? 0 : amount,
                    PaymentMethod = PaymentMethod.EFT,
                    FinancialYearId = fy.Id,
                    ImportBatchId = batch.Id,
                    StatementBalance = row.Balance,
                    ImportFingerprint = string.IsNullOrWhiteSpace(row.Fingerprint)
                        ? ImportFingerprint.Compute(row.Date, amount, row.Description, row.Reference)
                        : row.Fingerprint,
                    IsSquareDeposit = isSquareDeposit,
                    CreatedByUserId = userId,
                    CreatedByName = userName,
                    ModifiedByUserId = userId,
                    ModifiedByName = userName,
                    CreatedDate = now,
                    ModifiedDate = now
                };

                toInsert.Add(txn);

                if (row.Status == ImportRowStatus.Duplicate)
                    duplicates++;

                if (!isSquareDeposit && row.RememberCategory)
                    await _categorisationService.RememberAsync(txn.Description, categoryId);
                imported++;
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"{row.Description}: {ex.Message}");
            }
        }

        if (toInsert.Count > 0)
            _transactionRepository.InsertMany(toInsert);

        // Historical imports (including prior years) only adjust opening balances.
        // Current bank balance stays: tracking start balance + activity on/after that date.
        _financialYearService.RecalculateAllOpeningBalances();

        var squareIds = toInsert.Where(t => t.IsSquareDeposit).Select(t => t.Id).ToList();
        var (matched, needsReview) = squareIds.Count > 0
            ? await _reconciliationService.MatchTransactionsAsync(squareIds)
            : (0, 0);

        var unselectedDuplicates = request.Rows.Count(r => !r.IsSelected && r.Status == ImportRowStatus.Duplicate);
        duplicates = Math.Max(duplicates, unselectedDuplicates);

        sw.Stop();
        batch.TransactionsImported = imported;
        batch.TransactionsSkipped = skipped;
        batch.DuplicatesDetected = request.Rows.Count(r => r.Status == ImportRowStatus.Duplicate);
        batch.Errors = errors;
        batch.SquareMatched = matched;
        batch.SquareNeedsReview = needsReview;
        batch.ProcessingTimeMs = sw.Elapsed.TotalMilliseconds;
        batch.ErrorMessages = errorMessages;
        _importBatchRepository.Update(batch);

        return new ImportResult
        {
            Imported = imported,
            Skipped = skipped,
            Duplicates = batch.DuplicatesDetected,
            Errors = errors,
            SquareMatched = matched,
            SquareNeedsReview = needsReview,
            ProcessingTimeMs = batch.ProcessingTimeMs,
            BatchId = batch.Id,
            ErrorMessages = errorMessages
        };
    }

    public Task<(bool Success, string? Error)> UndoImportAsync(ObjectId batchId)
    {
        var batch = _importBatchRepository.GetById(batchId);
        if (batch is null)
            return Task.FromResult<(bool, string?)>((false, "Import batch not found."));

        if (batch.IsUndone)
            return Task.FromResult<(bool, string?)>((false, "This import has already been undone."));

        if (batch.SourceType == ImportSourceType.Square)
            return Task.FromResult<(bool, string?)>((false, "Use Square undo for Square imports."));

        var transactions = _transactionRepository.GetByImportBatch(batchId).ToList();
        if (transactions.Count == 0)
            return Task.FromResult<(bool, string?)>((false, "No transactions remain from this import."));

        var yearIds = transactions.Select(t => t.FinancialYearId).Distinct().ToList();
        foreach (var yearId in yearIds)
        {
            if (_financialYearService.IsLocked(yearId))
                return Task.FromResult<(bool, string?)>((false, "Cannot undo import — a financial year is locked."));
        }

        var user = _sessionService.CurrentUser;
        var now = DateTime.UtcNow;
        foreach (var txn in transactions)
        {
            if (txn.SquareDepositId is { } depositId)
            {
                var deposit = _squareDepositRepository.GetById(depositId);
                if (deposit is not null)
                {
                    deposit.Status = SquareDepositStatus.WaitingForMatch;
                    deposit.MatchedTransactionId = null;
                    deposit.ModifiedDate = now;
                    _squareDepositRepository.Update(deposit);
                }

                txn.SquareDepositId = null;
            }

            txn.IsDeleted = true;
            txn.DeletedAt = now;
            txn.ModifiedDate = now;
            txn.ModifiedByUserId = user?.Id ?? ObjectId.Empty;
            txn.ModifiedByName = user?.DisplayName ?? "System";
            _transactionRepository.Update(txn);
        }

        _financialYearService.RecalculateAllOpeningBalances();

        batch.IsUndone = true;
        batch.ModifiedDate = now;
        _importBatchRepository.Update(batch);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<IReadOnlyList<ImportHistoryItem>> GetHistoryAsync()
    {
        var items = _importBatchRepository.GetHistory()
            .Select(ToHistoryItem)
            .ToList();

        return Task.FromResult<IReadOnlyList<ImportHistoryItem>>(items);
    }

    public Task<ImportStatusSummary> GetImportStatusAsync()
    {
        var history = _importBatchRepository.GetHistory(10).Select(ToHistoryItem).ToList();
        // Transactions without a category still need treasurer review.
        var waitingReview = _transactionRepository.GetAll()
            .Count(t => !t.IsDeleted && t.CategoryId == ObjectId.Empty);
        var waitingMatch = _squareDepositRepository.GetUnmatched().Count();
        var lastBank = _importBatchRepository.GetLatestBySource(ImportSourceType.AnzBank);
        var lastSquare = _importBatchRepository.GetLatestBySource(ImportSourceType.Square);
        var recentDuplicates = history.Sum(h => h.DuplicatesDetected);

        return Task.FromResult(new ImportStatusSummary
        {
            RecentImportCount = history.Count,
            TransactionsWaitingReview = waitingReview,
            SquareDepositsWaitingMatch = waitingMatch,
            DuplicatesFound = recentDuplicates,
            LastBankImport = lastBank?.ImportedAt,
            LastSquareImport = lastSquare?.ImportedAt,
            RecentImports = history.Take(5).ToList()
        });
    }

    private static ImportHistoryItem ToHistoryItem(ImportBatch b)
    {
        var hasData = b.TransactionsImported > 0 && !b.IsUndone;
        return new ImportHistoryItem
        {
            Id = b.Id,
            ImportedAt = b.ImportedAt,
            FileName = b.FileName,
            SourceFormat = b.SourceFormat.ToString(),
            SourceType = b.SourceType switch
            {
                ImportSourceType.AnzBank => "ANZ Bank",
                ImportSourceType.Square => "Square",
                ImportSourceType.LegacyTreasurerReport => "Legacy Treasurer",
                _ => b.SourceType.ToString()
            },
            TransactionsImported = b.TransactionsImported,
            TransactionsSkipped = b.TransactionsSkipped,
            DuplicatesDetected = b.DuplicatesDetected,
            Errors = b.Errors,
            ProcessingTimeMs = b.ProcessingTimeMs,
            UserName = b.UserName,
            UndoAvailable = hasData,
            IsUndone = b.IsUndone
        };
    }

    private static StatementParseResult ParseFile(string filePath, AnzColumnMapping? mapping)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".csv" => AnzCsvStatementParser.Parse(filePath, mapping),
            ".xlsx" => AnzExcelStatementParser.Parse(filePath, mapping),
            ".xls" => throw new NotSupportedException("Legacy .xls is not supported. Save as .xlsx or CSV."),
            ".pdf" or ".ofx" or ".qif" or ".json" =>
                throw new NotSupportedException($"{ext} import is planned for a future version. Use an ANZ CSV export."),
            _ => throw new NotSupportedException($"Unsupported file type '{ext}'. Use the ANZ CSV export from internet banking.")
        };
    }


    private static void DetectDuplicate(
        ImportPreviewRow preview,
        ParsedStatementRow row,
        IReadOnlyDictionary<string, TransactionModel> byFingerprint,
        IReadOnlyList<TransactionModel> existingInRange)
    {
        if (byFingerprint.TryGetValue(row.Fingerprint, out var exact))
        {
            preview.Status = ImportRowStatus.Duplicate;
            preview.DuplicateConfidence = 1.0;
            preview.MatchedTransactionId = exact.Id;
            preview.IsSelected = false;
            return;
        }

        var amount = row.Credit > 0 ? row.Credit : row.Debit;
        var candidates = existingInRange.Where(t =>
        {
            var txnAmount = t.IncomeAmount > 0 ? t.IncomeAmount : t.ExpenseAmount;
            return t.Date.Date == row.Date.Date && txnAmount == amount;
        }).ToList();

        foreach (var candidate in candidates)
        {
            var score = 0.5;
            if (string.Equals(candidate.Description.Trim(), row.Description.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 0.3;
            if (!string.IsNullOrWhiteSpace(row.Reference) &&
                string.Equals(candidate.Reference.Trim(), row.Reference.Trim(), StringComparison.OrdinalIgnoreCase))
                score += 0.2;

            if (score >= 0.8)
            {
                preview.Status = ImportRowStatus.Duplicate;
                preview.DuplicateConfidence = score;
                preview.MatchedTransactionId = candidate.Id;
                preview.IsSelected = false;
                return;
            }

            if (score >= 0.5)
            {
                preview.Status = ImportRowStatus.Matched;
                preview.DuplicateConfidence = score;
                preview.MatchedTransactionId = candidate.Id;
            }
        }
    }
}
