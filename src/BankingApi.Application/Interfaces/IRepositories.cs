using BankingApi.Domain.Entities;

namespace BankingApi.Application.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default);
    Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedByAccountIdAsync(
        Guid accountId, int page, int pageSize, CancellationToken ct = default);
}
