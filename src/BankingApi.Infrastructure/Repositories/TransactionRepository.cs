using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly BankingDbContext _db;

    public TransactionRepository(BankingDbContext db) => _db = db;

    public async Task<Transaction?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default)
        => await _db.Transactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    public async Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedByAccountIdAsync(
        Guid accountId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
