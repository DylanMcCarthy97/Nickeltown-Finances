using LiteDB;
using NickeltownFinance.Core.Enums;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptImportBatchService : IReceiptImportBatchService
{
    private readonly IReceiptImportBatchRepository _batchRepository;
    private readonly IReceiptImportItemRepository _itemRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISessionService _sessionService;

    public ReceiptImportBatchService(
        IReceiptImportBatchRepository batchRepository,
        IReceiptImportItemRepository itemRepository,
        IAttachmentRepository attachmentRepository,
        ITransactionRepository transactionRepository,
        ISessionService sessionService)
    {
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _attachmentRepository = attachmentRepository;
        _transactionRepository = transactionRepository;
        _sessionService = sessionService;
    }

    public Task<ReceiptImportBatch> CreateBatchAsync(ReceiptImportSource source, string label)
    {
        var user = _sessionService.CurrentUser;
        var batch = new ReceiptImportBatch
        {
            Source = source,
            Label = label,
            ImportedAt = DateTime.UtcNow,
            ImportedByUserId = user?.Id ?? ObjectId.Empty,
            ImportedByName = user?.DisplayName ?? "System"
        };
        _batchRepository.Insert(batch);
        return Task.FromResult(batch);
    }

    public Task UndoBatchAsync(ObjectId batchId)
    {
        var batch = _batchRepository.GetById(batchId)
            ?? throw new InvalidOperationException("Batch not found.");

        if (batch.IsUndone) return Task.CompletedTask;

        foreach (var item in _itemRepository.GetAll().Where(i => i.ImportBatchId == batchId))
        {
            if (item.ResultAttachmentId is { } attachmentId && attachmentId != ObjectId.Empty)
                _attachmentRepository.Delete(attachmentId);

            if (item.MatchedTransactionId is { } matchedId &&
                matchedId != ObjectId.Empty &&
                item.Status == ReceiptImportStatus.Committed &&
                item.Source == ReceiptImportSource.Desktop)
            {
                var txn = _transactionRepository.GetById(matchedId);
                if (txn is not null && txn.Notes.Contains("Created from receipt import", StringComparison.Ordinal))
                    _transactionRepository.Delete(txn.Id);
            }

            item.Status = ReceiptImportStatus.Ignored;
            _itemRepository.Update(item);
        }

        batch.IsUndone = true;
        _batchRepository.Update(batch);
        return Task.CompletedTask;
    }
}
