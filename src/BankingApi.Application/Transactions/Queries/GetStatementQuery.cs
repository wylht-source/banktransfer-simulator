using BankingApi.Application.Common;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Transactions.Queries;

public record GetStatementQuery(Guid AccountId, string RequestingUserId, int Page = 1, int PageSize = 20);

public record TransactionDto(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string Description,
    DateTime CreatedAt,
    Guid? FromAccountId,
    Guid? ToAccountId
);

public class GetStatementHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;

    public GetStatementHandler(IAccountRepository accounts, ITransactionRepository transactions)
    {
        _accounts = accounts;
        _transactions = transactions;
    }

    public async Task<PagedResult<TransactionDto>> HandleAsync(GetStatementQuery query, CancellationToken ct = default)
    {
        var account = await _accounts.GetByIdAsync(query.AccountId, ct)
            ?? throw new DomainException($"Account {query.AccountId} not found.");

        if (account.OwnerId != query.RequestingUserId)
            throw new DomainException("Access denied.");

        var (items, totalCount) = await _transactions.GetPagedByAccountIdAsync(
            query.AccountId, query.Page, query.PageSize, ct);

        var dtos = items.Select(t => new TransactionDto(
            t.Id, t.Type, t.Amount, t.Description, t.CreatedAt,
            t.FromAccountId, t.ToAccountId));

        return new PagedResult<TransactionDto>(dtos, query.Page, query.PageSize, totalCount);
    }
}