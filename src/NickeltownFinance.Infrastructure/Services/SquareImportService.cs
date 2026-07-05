using System.Diagnostics;
using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Import;

namespace NickeltownFinance.Infrastructure.Services;

public class SquareImportService : ISquareImportService
{
    private readonly ISquareDepositRepository _depositRepository;
    private readonly ISquareTransactionRepository _transactionRepository;
    private readonly IImportBatchRepository _importBatchRepository;
    private readonly IReconciliationService _reconciliationService;
    private readonly ISessionService _sessionService;
    private readonly ITransactionRepository _bankTransactionRepository;

    public SquareImportService(
        ISquareDepositRepository depositRepository,
        ISquareTransactionRepository transactionRepository,
        IImportBatchRepository importBatchRepository,
        IReconciliationService reconciliationService,
        ISessionService sessionService,
        ITransactionRepository bankTransactionRepository)
    {
        _depositRepository = depositRepository;
        _transactionRepository = transactionRepository;
        _importBatchRepository = importBatchRepository;
        _reconciliationService = reconciliationService;
        _sessionService = sessionService;
        _bankTransactionRepository = bankTransactionRepository;
    }

    public Task<SquareImportAnalyseResult> AnalyseAsync(string filePath)
    {
        try
        {
            var preview = SquareCsvParser.Parse(filePath);
            var existingFingerprints = _depositRepository.GetActive()
                .Where(d => !string.IsNullOrWhiteSpace(d.ImportFingerprint))
                .Select(d => d.ImportFingerprint)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingDepositIds = _depositRepository.GetActive()
                .Select(d => d.DepositId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var deposit in preview.Deposits)
            {
                if (existingFingerprints.Contains(deposit.Fingerprint) ||
                    existingDepositIds.Contains(deposit.DepositId))
                {
                    deposit.Status = ImportRowStatus.Duplicate;
                    deposit.IsSelected = false;
                }
            }

            return Task.FromResult(new SquareImportAnalyseResult { Preview = preview });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SquareImportAnalyseResult { FailureReason = ex.Message });
        }
    }

    public async Task<ImportResult> CommitAsync(SquareImportCommitRequest request)
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
            SourceFormat = StatementFormat.Csv,
            SourceType = ImportSourceType.Square,
            BankName = "Square",
            UserId = userId,
            UserName = userName
        };
        _importBatchRepository.Insert(batch);

        var depositsToInsert = new List<SquareDeposit>();
        var transactionsToInsert = new List<SquareTransaction>();

        foreach (var row in request.Deposits)
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
                if (_depositRepository.GetByExternalDepositId(row.DepositId) is not null)
                {
                    duplicates++;
                    skipped++;
                    continue;
                }

                var deposit = new SquareDeposit
                {
                    DepositId = row.DepositId.Trim(),
                    DepositDate = row.DepositDate.Date,
                    GrossAmount = row.GrossAmount,
                    Fees = row.Fees,
                    NetAmount = row.NetAmount,
                    TransactionCount = row.TransactionCount,
                    Status = SquareDepositStatus.WaitingForMatch,
                    ImportBatchId = batch.Id,
                    ImportFingerprint = row.Fingerprint,
                    CreatedDate = now,
                    ModifiedDate = now
                };

                // Pre-assign Id so child transactions can link before insert.
                deposit.Id = ObjectId.NewObjectId();
                depositsToInsert.Add(deposit);

                foreach (var line in row.Lines)
                {
                    transactionsToInsert.Add(new SquareTransaction
                    {
                        DepositId = deposit.Id,
                        ExternalDepositId = deposit.DepositId,
                        Date = line.Date.Date,
                        DepositDate = line.DepositDate?.Date ?? deposit.DepositDate,
                        CustomerName = line.CustomerName.Trim(),
                        Description = line.Description.Trim(),
                        Category = line.Category.Trim(),
                        GrossAmount = line.GrossAmount,
                        Fees = line.Fees,
                        NetAmount = line.NetAmount,
                        PaymentMethod = line.PaymentMethod.Trim(),
                        ExternalTransactionId = line.ExternalTransactionId.Trim(),
                        ImportFingerprint = line.Fingerprint,
                        ImportedTimestamp = now,
                        ImportBatchId = batch.Id
                    });
                }

                imported++;
            }
            catch (Exception ex)
            {
                errors++;
                errorMessages.Add($"{row.DepositId}: {ex.Message}");
            }
        }

        if (depositsToInsert.Count > 0)
            _depositRepository.InsertMany(depositsToInsert);
        if (transactionsToInsert.Count > 0)
            _transactionRepository.InsertMany(transactionsToInsert);

        // Try to match any unmatched ANZ Square transfers now that deposits exist.
        var unmatchedAnz = _bankTransactionRepository.GetAll()
            .Where(t => !t.IsDeleted && t.IsSquareDeposit && t.SquareDepositId is null)
            .Select(t => t.Id)
            .ToList();

        var (matched, needsReview) = unmatchedAnz.Count > 0
            ? await _reconciliationService.MatchTransactionsAsync(unmatchedAnz)
            : (0, 0);

        sw.Stop();
        batch.TransactionsImported = imported;
        batch.TransactionsSkipped = skipped;
        batch.DuplicatesDetected = request.Deposits.Count(d => d.Status == ImportRowStatus.Duplicate);
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

        if (batch.SourceType != ImportSourceType.Square)
            return Task.FromResult<(bool, string?)>((false, "This is not a Square import."));

        if (batch.IsUndone)
            return Task.FromResult<(bool, string?)>((false, "This import has already been undone."));

        var deposits = _depositRepository.GetByImportBatch(batchId).ToList();
        var transactions = _transactionRepository.GetByImportBatch(batchId).ToList();
        if (deposits.Count == 0 && transactions.Count == 0)
            return Task.FromResult<(bool, string?)>((false, "No Square data remains from this import."));

        var now = DateTime.UtcNow;
        foreach (var deposit in deposits)
        {
            if (deposit.MatchedTransactionId is { } txnId && txnId != ObjectId.Empty)
            {
                var bankTxn = _bankTransactionRepository.GetById(txnId);
                if (bankTxn is not null)
                {
                    bankTxn.SquareDepositId = null;
                    bankTxn.ModifiedDate = now;
                    _bankTransactionRepository.Update(bankTxn);
                }
            }

            deposit.IsDeleted = true;
            deposit.DeletedAt = now;
            deposit.Status = SquareDepositStatus.WaitingForMatch;
            deposit.MatchedTransactionId = null;
            deposit.ModifiedDate = now;
            _depositRepository.Update(deposit);
        }

        foreach (var txn in transactions)
        {
            txn.IsDeleted = true;
            txn.ModifiedDate = now;
            _transactionRepository.Update(txn);
        }

        batch.IsUndone = true;
        batch.ModifiedDate = now;
        _importBatchRepository.Update(batch);

        return Task.FromResult<(bool, string?)>((true, null));
    }
}
