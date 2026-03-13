using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Transactions.Commands;

public record DepositCommand(Guid AccountId, decimal Amount, string Description, Guid IdempotencyKey);

public record DepositResult(Guid TransactionId, decimal NewBalance, bool WasDuplicate);

public class DepositHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;

    public DepositHandler(IAccountRepository accounts, ITransactionRepository transactions)
    {
        _accounts = accounts;
        _transactions = transactions;
    }

    public async Task<DepositResult> HandleAsync(DepositCommand cmd, CancellationToken ct = default)
    {
        // Idempotency check
        var existing = await _transactions.GetByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (existing is not null)
        {
            var acc = await _accounts.GetByIdAsync(cmd.AccountId, ct);
            return new DepositResult(existing.Id, acc!.Balance, WasDuplicate: true);
        }

        var account = await _accounts.GetByIdAsync(cmd.AccountId, ct)
            ?? throw new DomainException($"Account {cmd.AccountId} not found.");

        account.Deposit(cmd.Amount, cmd.Description, cmd.IdempotencyKey);

        await _accounts.SaveChangesAsync(ct);

        var transaction = account.Transactions.Last();
        return new DepositResult(transaction.Id, account.Balance, WasDuplicate: false);
    }
}
