using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Accounts.Queries;

public record GetAccountQuery(Guid AccountId, string RequestingUserId);

public record GetAccountResult(Guid Id, string AccountNumber, string OwnerName, decimal Balance, DateTime CreatedAt);

public class GetAccountHandler
{
    private readonly IAccountRepository _accounts;

    public GetAccountHandler(IAccountRepository accounts) => _accounts = accounts;

    public async Task<GetAccountResult> HandleAsync(GetAccountQuery query, CancellationToken ct = default)
    {
        var account = await _accounts.GetByIdAsync(query.AccountId, ct)
            ?? throw new DomainException($"Account {query.AccountId} not found.");

        if (account.OwnerId != query.RequestingUserId)
            throw new DomainException("Access denied.");

        return new GetAccountResult(account.Id, account.AccountNumber, account.OwnerName, account.Balance, account.CreatedAt);
    }
}