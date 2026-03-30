using BankingApi.Domain.Entities;

namespace BankingApi.Application.Interfaces;

public interface ILoanDocumentRepository
{
    Task AddAsync(LoanDocument document, CancellationToken ct = default);
    Task<LoanDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<LoanDocument>> GetByLoanIdAsync(Guid loanId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
