using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Infrastructure.Repositories;

public class LoanDocumentRepository(BankingDbContext db) : ILoanDocumentRepository
{
    public async Task AddAsync(LoanDocument document, CancellationToken ct = default)
        => await db.LoanDocuments.AddAsync(document, ct);

    public async Task<LoanDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.LoanDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IEnumerable<LoanDocument>> GetByLoanIdAsync(Guid loanId, CancellationToken ct = default)
        => await db.LoanDocuments
            .Where(d => d.LoanId == loanId)
            .OrderBy(d => d.UploadedAt)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
