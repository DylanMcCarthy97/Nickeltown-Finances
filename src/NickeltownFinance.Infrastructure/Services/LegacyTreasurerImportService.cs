using System.Diagnostics;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.FinancialYears;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Import;
using TransactionModel = NickeltownFinance.Core.Models.Transaction;

namespace NickeltownFinance.Infrastructure.Services;

public class LegacyTreasurerImportService : ILegacyTreasurerImportService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITreasurerMonthSnapshotRepository _snapshotRepository;
    private readonly IImportBatchRepository _importBatchRepository;
    private readonly IFinancialYearService _financialYearService;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategorisationService _categorisationService;
    private readonly ISessionService _sessionService;

    public LegacyTreasurerImportService(
        ITransactionRepository transactionRepository,
        ITreasurerMonthSnapshotRepository snapshotRepository,
        IImportBatchRepository importBatchRepository,
        IFinancialYearService financialYearService,
        ICategoryRepository categoryRepository,
        ICategorisationService categorisationService,
        ISessionService sessionService)
    {
        _transactionRepository = transactionRepository;
        _snapshotRepository = snapshotRepository;
        _importBatchRepository = importBatchRepository;
        _financialYearService = financialYearService;
        _categoryRepository = categoryRepository;
        _categorisationService = categorisationService;
        _sessionService = sessionService;
    }

    public async Task<LegacyTreasurerAnalyseResult> AnalyseAsync(string filePath)
    {
        try
        {
            var parsed = LegacyTreasurerReportParser.Parse(filePath);
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
                var hasCategory = suggestion.CategoryId is not null && suggestion.CategoryId != ObjectId.Empty;

                var preview = new ImportPreviewRow
                {
                    Date = row.Date,
                    Description = row.Description,
                    Debit = row.Debit,
                    Credit = row.Credit,
                    Fingerprint = row.Fingerprint,
                    SuggestedCategoryId = suggestion.CategoryId,
                    SuggestedCategoryName = hasCategory ? suggestion.CategoryName : string.Empty,
                    Status = hasCategory ? ImportRowStatus.New : ImportRowStatus.NeedsReview,
                    IsSelected = true
                };

                DetectDuplicate(preview, byFingerprint, existing);
                previewRows.Add(preview);
            }

            return new LegacyTreasurerAnalyseResult
            {
                Preview = new LegacyTreasurerParseResult
                {
                    FileName = parsed.FileName,
                    Months = parsed.Months,
                    Rows = previewRows,
                    Warnings = parsed.Warnings
                }
            };
        }
        catch (Exception ex)
        {
            return new LegacyTreasurerAnalyseResult { FailureReason = ex.Message };
        }
    }

    public async Task<ImportResult> CommitAsync(LegacyTreasurerCommitRequest request)
    {
        var sw = Stopwatch.StartNew();
        var imported = 0;
        var skipped = 0;
        var duplicates = 0;
        var errors = 0;
        var snapshotsImported = 0;
        var errorMessages = new List<string>();

        var user = _sessionService.CurrentUser;
        var userId = user?.Id ?? ObjectId.Empty;
        var userName = user?.DisplayName ?? "System";
        var now = DateTime.UtcNow;

        var batch = new ImportBatch
        {
            ImportedAt = now,
            FileName = request.FileName,
            SourceFormat = StatementFormat.Excel,
            SourceType = ImportSourceType.LegacyTreasurerReport,
            BankName = "Legacy Treasurer Report",
            UserId = userId,
            UserName = userName
        };
        _importBatchRepository.Insert(batch);

        var selectedRows = request.Rows
            .Where(r => r.IsSelected && r.Status != ImportRowStatus.Ignored && r.Status != ImportRowStatus.Duplicate)
            .OrderBy(r => r.Date)
            .ToList();

        var yearsCreated = selectedRows.Count > 0
            ? _financialYearService.EnsureYearsForDates(selectedRows.Select(r => r.Date))
            : 0;

        SeedLegacyOpeningBalances(request.Months);

        var toInsert = new List<TransactionModel>();

        foreach (var row in request.Rows)
        {
            if (!row.IsSelected || row.Status == ImportRowStatus.Ignored)
            {
                skipped++;
                continue;
            }

            if (row.Status == ImportRowStatus.Duplicate)
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

                var categoryId = row.SuggestedCategoryId ?? ObjectId.Empty;
                if (categoryId == ObjectId.Empty)
                {
                    var fallback = _categoryRepository.GetByType(isIncome ? CategoryType.Income : CategoryType.Expense)
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
                    ImportFingerprint = string.IsNullOrWhiteSpace(row.Fingerprint)
                        ? ImportFingerprint.Compute(row.Date, amount, row.Description, row.Reference)
                        : row.Fingerprint,
                    CreatedByUserId = userId,
                    CreatedByName = userName,
                    ModifiedByUserId = userId,
                    ModifiedByName = userName,
                    CreatedDate = now,
                    ModifiedDate = now
                };

                toInsert.Add(txn);

                if (row.RememberCategory)
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

        foreach (var month in request.Months.Where(m => !m.IsSkipped))
        {
            try
            {
                _snapshotRepository.Upsert(new TreasurerMonthSnapshot
                {
                    Year = month.Year,
                    Month = month.Month,
                    PeriodFrom = month.PeriodFrom,
                    PeriodTo = month.PeriodTo,
                    OpeningBankBalance = month.OpeningBankBalance,
                    ClosingBankBalance = month.ClosingBankBalance,
                    CashOnHand = month.CashOnHand,
                    ShireBonds = month.ShireBonds,
                    PayPalBalance = month.PayPalBalance,
                    SheetName = month.SheetName,
                    ImportBatchId = batch.Id
                });
                snapshotsImported++;
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"Month {month.MonthLabel}: {ex.Message}");
            }
        }

        _financialYearService.RecalculateAfterLegacyImport(selectedRows.Select(r => r.Date));

        sw.Stop();
        batch.TransactionsImported = imported;
        batch.TransactionsSkipped = skipped;
        batch.DuplicatesDetected = request.Rows.Count(r => r.Status == ImportRowStatus.Duplicate);
        batch.Errors = errors;
        batch.ProcessingTimeMs = sw.Elapsed.TotalMilliseconds;
        batch.ErrorMessages = errorMessages;
        if (yearsCreated > 0)
            batch.ErrorMessages.Add($"Created {yearsCreated} financial year(s) from legacy transaction dates.");
        if (snapshotsImported > 0)
            batch.ErrorMessages.Add($"Saved holdings for {snapshotsImported} month(s).");
        _importBatchRepository.Update(batch);

        return new ImportResult
        {
            Imported = imported,
            Skipped = skipped,
            Duplicates = batch.DuplicatesDetected,
            Errors = errors,
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

        if (batch.SourceType != ImportSourceType.LegacyTreasurerReport)
            return Task.FromResult<(bool, string?)>((false, "This is not a legacy treasurer import."));

        var transactions = _transactionRepository.GetByImportBatch(batchId).ToList();
        var snapshots = _snapshotRepository.GetAll()
            .Where(s => s.ImportBatchId == batchId)
            .ToList();

        if (transactions.Count == 0 && snapshots.Count == 0)
            return Task.FromResult<(bool, string?)>((false, "No data remains from this import."));

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
            txn.IsDeleted = true;
            txn.DeletedAt = now;
            txn.ModifiedDate = now;
            txn.ModifiedByUserId = user?.Id ?? ObjectId.Empty;
            txn.ModifiedByName = user?.DisplayName ?? "System";
            _transactionRepository.Update(txn);
        }

        foreach (var snapshot in snapshots)
            _snapshotRepository.Delete(snapshot.Id);

        _financialYearService.RecalculateAllOpeningBalances();

        batch.IsUndone = true;
        batch.ModifiedDate = now;
        _importBatchRepository.Update(batch);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    private static void DetectDuplicate(
        ImportPreviewRow preview,
        IReadOnlyDictionary<string, TransactionModel> byFingerprint,
        IReadOnlyList<TransactionModel> existingInRange)
    {
        if (byFingerprint.TryGetValue(preview.Fingerprint, out var exact))
        {
            preview.Status = ImportRowStatus.Duplicate;
            preview.DuplicateConfidence = 1.0;
            preview.MatchedTransactionId = exact.Id;
            preview.IsSelected = false;
            return;
        }

        var amount = preview.Credit > 0 ? preview.Credit : preview.Debit;
        var candidates = existingInRange.Where(t =>
        {
            var txnAmount = t.IncomeAmount > 0 ? t.IncomeAmount : t.ExpenseAmount;
            return t.Date.Date == preview.Date.Date &&
                   txnAmount == amount &&
                   string.Equals(t.Description.Trim(), preview.Description.Trim(), StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (candidates.Count == 0)
            return;

        preview.Status = ImportRowStatus.Duplicate;
        preview.DuplicateConfidence = 0.9;
        preview.MatchedTransactionId = candidates[0].Id;
        preview.IsSelected = false;
    }

    private void SeedLegacyOpeningBalances(IReadOnlyList<LegacyTreasurerMonthSummary> months)
    {
        var seededYears = new HashSet<ObjectId>();
        foreach (var month in months.Where(m => !m.IsSkipped).OrderBy(m => m.Year).ThenBy(m => m.Month))
        {
            if (month.OpeningBankBalance == 0)
                continue;

            var anchorDate = month.PeriodFrom?.Date ?? new DateTime(month.Year, month.Month, 1);
            var fy = _financialYearService.EnsureYearForDate(anchorDate);
            if (seededYears.Contains(fy.Id))
                continue;

            if (_financialYearService.TrySeedLegacyYearOpening(fy.Id, anchorDate, month.OpeningBankBalance))
                seededYears.Add(fy.Id);
        }
    }
}
