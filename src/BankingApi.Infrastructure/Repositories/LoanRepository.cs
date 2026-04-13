using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingApi.Infrastructure.Repositories;

public class LoanRepository : ILoanRepository
{
    private readonly BankingDbContext _db;
    private readonly ILogger<LoanRepository> _logger;

    public LoanRepository(BankingDbContext db, ILogger<LoanRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Loan?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default)
    => await _db.Loans
        .FirstOrDefaultAsync(l => l.IdempotencyKey == key, ct);

    public async Task AddAsync(Loan loan, CancellationToken ct = default)
        => await _db.Loans.AddAsync(loan, ct);

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Loans
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<Loan?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default)
        => await _db.Loans
            .Include(l => l.ApprovalHistory)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<(IEnumerable<Loan> Loans, int TotalCount)> GetByClientIdAsync(
        string clientId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Loans
            .Where(l => l.ClientId == clientId)
            .OrderByDescending(l => l.RequestedAt);

        var totalCount = await query.CountAsync(ct);

        var loans = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (loans, totalCount);
    }

    public async Task<(IEnumerable<Loan> Loans, int TotalCount)> GetPendingByRolesAsync(
        IEnumerable<string> roles, int page, int pageSize, CancellationToken ct = default)
    {
        var roleList = roles.ToList();

        var query = _db.Loans
            .Where(l => l.Status == LoanStatus.PendingApproval
                     && roleList.Contains(l.RequiredApprovalRole))
            .OrderBy(l => l.RequestedAt); // oldest first — approvers see the queue in order

        var totalCount = await query.CountAsync(ct);

        var loans = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (loans, totalCount);
    }

    public async Task<(IEnumerable<Loan> Loans, int TotalCount)> GetDecidedByRolesAsync(
        IEnumerable<string> roles, int page, int pageSize, CancellationToken ct = default)
    {
        var roleList = roles.ToList();
        var decidedStatuses = new[] { LoanStatus.Approved, LoanStatus.Rejected };

        var query = _db.Loans
            .Where(l => decidedStatuses.Contains(l.Status)
                    && roleList.Contains(l.RequiredApprovalRole))
            .OrderByDescending(l => l.ApprovedAt ?? l.RequestedAt);

        var totalCount = await query.CountAsync(ct);

        var loans = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (loans, totalCount);
    }
    
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in _db.ChangeTracker.Entries<LoanApprovalHistory>())
        {
            // LoanApprovalHistory is append-only — same pattern as Transaction
            var existsInDb = await _db.LoanApprovalHistories
                .AsNoTracking()
                .AnyAsync(h => h.Id == entry.Entity.Id, ct);

            if (!existsInDb)
            {
                entry.State = EntityState.Added;
                _logger.LogInformation("LoanApprovalHistory {Id} → Added (new)", entry.Entity.Id);
            }
            else
            {
                entry.State = EntityState.Unchanged;
                _logger.LogInformation("LoanApprovalHistory {Id} → Unchanged (existing)", entry.Entity.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
