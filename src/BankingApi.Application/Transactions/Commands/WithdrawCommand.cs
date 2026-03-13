using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Transactions.Commands;

public record WithdrawCommand(Guid AccountId, decimal Amount, string Description, Guid IdempotencyKey);

public record WithdrawResult(Guid TransactionId, decimal NewBalance, bool WasDuplicate);

public class WithdrawHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;

    public WithdrawHandler(IAccountRepository accounts, ITransactionRepository transactions)
    {
        _accounts = accounts;
        _transactions = transactions;
    }

    public async Task<WithdrawResult> HandleAsync(WithdrawCommand cmd, CancellationToken ct = default)
    {
        var existing = await _transactions.GetByIdempotencyKeyAsync(cmd.IdempotencyKey, ct);
        if (existing is not null)
        {
            var acc = await _accounts.GetByIdAsync(cmd.AccountId, ct);
            return new WithdrawResult(existing.Id, acc!.Balance, WasDuplicate: true);
        }

        var account = await _accounts.GetByIdAsync(cmd.AccountId, ct)
            ?? throw new DomainException($"Account {cmd.AccountId} not found.");

        account.Withdraw(cmd.Amount, cmd.Description, cmd.IdempotencyKey);

        await _accounts.SaveChangesAsync(ct);

        var transaction = account.Transactions.Last();
        return new WithdrawResult(transaction.Id, account.Balance, WasDuplicate: false);
    }
}
