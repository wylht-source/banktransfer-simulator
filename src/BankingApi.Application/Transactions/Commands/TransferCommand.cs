using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Transactions.Commands;

public record TransferCommand(Guid FromAccountId, Guid ToAccountId, decimal Amount, string Description, Guid IdempotencyKey, string RequestingUserId);

public record TransferResult(Guid TransactionId, decimal FromAccountBalance, bool WasDuplicate);

public class TransferHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;

    public TransferHandler(IAccountRepository accounts, ITransactionRepository transactions)
    {
        _accounts = accounts;
        _transactions = transactions;
    }

    public async Task<TransferResult> HandleAsync(TransferCommand cmd, CancellationToken ct = default)
    {
        var existing = await _transactions.GetByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (existing is not null)
        {
            var acc = await _accounts.GetByIdAsync(cmd.FromAccountId, ct);
            return new TransferResult(existing.Id, acc!.Balance, WasDuplicate: true);
        }

        var from = await _accounts.GetByIdAsync(cmd.FromAccountId, ct)
            ?? throw new DomainException($"Account {cmd.FromAccountId} not found.");
        if (from.OwnerId != cmd.RequestingUserId)
            throw new DomainException("Access denied.");

        var to = await _accounts.GetByIdAsync(cmd.ToAccountId, ct)
            ?? throw new DomainException($"Account {cmd.ToAccountId} not found.");

        if (cmd.FromAccountId == cmd.ToAccountId)
            throw new DomainException("Cannot transfer to the same account.");

        from.Debit(cmd.Amount, $"Transfer to {to.AccountNumber}: {cmd.Description}", toAccountId: to.Id, idempotencyKey: cmd.IdempotencyKey);
        to.Credit(cmd.Amount, $"Transfer from {from.AccountNumber}: {cmd.Description}", fromAccountId: from.Id);

        await _accounts.SaveChangesAsync(ct);

        var transaction = from.Transactions.Last();
        return new TransferResult(transaction.Id, from.Balance, WasDuplicate: false);
    }
}