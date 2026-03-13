using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingApi.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _db;
    private readonly ILogger<AccountRepository> _logger;

    public AccountRepository(BankingDbContext db, ILogger<AccountRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
        => await _db.Accounts.AddAsync(account, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in _db.ChangeTracker.Entries<Transaction>())
        {
            // Check if this transaction actually exists in the DB
            var existsInDb = await _db.Transactions
                .AsNoTracking()
                .AnyAsync(t => t.Id == entry.Entity.Id);

            if (!existsInDb)
            {
                // New transaction — force INSERT
                entry.State = EntityState.Added;
                _logger.LogInformation("Transaction {Id} → Added (new)", entry.Entity.Id);
            }
            else
            {
                // Existing transaction — never update
                entry.State = EntityState.Unchanged;
                _logger.LogInformation("Transaction {Id} → Unchanged (existing)", entry.Entity.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}