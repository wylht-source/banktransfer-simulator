using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;

namespace BankingApi.Application.Accounts.Commands;

public record CreateAccountCommand(string OwnerName, string OwnerId);

public record CreateAccountResult(Guid Id, string AccountNumber, string OwnerName, DateTime CreatedAt);

public class CreateAccountHandler
{
    private readonly IAccountRepository _accounts;

    public CreateAccountHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<CreateAccountResult> HandleAsync(CreateAccountCommand cmd, CancellationToken ct = default)
    {
        var account = Account.Create(cmd.OwnerName, cmd.OwnerId);
        await _accounts.AddAsync(account, ct);
        await _accounts.SaveChangesAsync(ct);
        return new CreateAccountResult(account.Id, account.AccountNumber, account.OwnerName, account.CreatedAt);
    }
}