using LiteDB;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Infrastructure.Import;

namespace NickeltownFinance.Infrastructure.Services;

public class SquareDepositService : ISquareDepositService
{
    private readonly ITransactionRepository _bankTransactionRepository;
    private readonly ISquareDepositRepository _depositRepository;
    private readonly ISquareTransactionRepository _squareTransactionRepository;

    public SquareDepositService(
        ITransactionRepository bankTransactionRepository,
        ISquareDepositRepository depositRepository,
        ISquareTransactionRepository squareTransactionRepository)
    {
        _bankTransactionRepository = bankTransactionRepository;
        _depositRepository = depositRepository;
        _squareTransactionRepository = squareTransactionRepository;
    }

    public Task<SquareDepositDetailDto?> GetDetailForBankTransactionAsync(ObjectId transactionId)
    {
        var txn = _bankTransactionRepository.GetById(transactionId);
        if (txn is null || txn.IsDeleted || txn.SquareDepositId is not { } depositId)
            return Task.FromResult<SquareDepositDetailDto?>(null);

        var deposit = _depositRepository.GetById(depositId);
        if (deposit is null || deposit.IsDeleted)
            return Task.FromResult<SquareDepositDetailDto?>(null);

        var lines = _squareTransactionRepository.GetByDeposit(deposit.Id)
            .OrderBy(t => t.TransactionTime == default ? t.Date : t.TransactionTime)
            .ThenBy(t => t.Description)
            .ToList();

        var groups = lines
            .GroupBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var items = g.Select(line => new SquareDepositLineItemDto
                {
                    Description = line.Description,
                    CustomerName = line.CustomerName,
                    Amount = line.GrossAmount,
                    TransactionTime = line.TransactionTime == default ? line.Date : line.TransactionTime,
                    PaymentId = string.IsNullOrWhiteSpace(line.PaymentId) ? null : line.PaymentId
                }).ToList();

                return new SquareDepositItemGroupDto
                {
                    Description = g.Key,
                    DisplayTitle = SquareDescriptionHelper.ToGroupDisplayName(g.Key, items.Count),
                    TotalAmount = items.Sum(i => i.Amount),
                    Items = items
                };
            })
            .ToList();

        return Task.FromResult<SquareDepositDetailDto?>(new SquareDepositDetailDto
        {
            DepositDate = deposit.DepositDate,
            GrossSales = deposit.GrossAmount,
            Fees = deposit.Fees,
            NetDeposit = deposit.NetAmount,
            Groups = groups
        });
    }
}
