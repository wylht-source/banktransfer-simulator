using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Accounts.Queries;

public record GetAccountByOwnerQuery(string OwnerId);

public class GetAccountByOwnerHandler
{
    private readonly IAccountRepository _accounts;

    public GetAccountByOwnerHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<GetAccountResult> HandleAsync(GetAccountByOwnerQuery query, CancellationToken ct = default)
    {
        var account = await _accounts.GetByOwnerIdAsync(query.OwnerId, ct)
            ?? throw new DomainException("Account not found for the authenticated user.");

        return new GetAccountResult(account.Id, account.AccountNumber, account.OwnerName, account.Balance, account.CreatedAt);
    }
}