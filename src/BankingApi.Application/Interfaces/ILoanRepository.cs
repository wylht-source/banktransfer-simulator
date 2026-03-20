using BankingApi.Domain.Entities;

namespace BankingApi.Application.Interfaces;

public interface ILoanRepository
{
    Task AddAsync(Loan loan, CancellationToken ct = default);
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Loan?> GetByIdWithHistoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns loans belonging to a specific client, ordered by RequestedAt descending.
    /// </summary>
    Task<(IEnumerable<Loan> Loans, int TotalCount)> GetByClientIdAsync(
        string clientId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns PendingApproval loans whose RequiredApprovalRole is in the given set.
    /// Used by GetPendingLoansQuery to respect the approval hierarchy.
    /// </summary>
    Task<(IEnumerable<Loan> Loans, int TotalCount)> GetPendingByRolesAsync(
        IEnumerable<string> roles, int page, int pageSize, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns Approved and Rejected loans whose RequiredApprovalRole is in the given set.
    /// </summary>
    Task<(IEnumerable<Loan> Loans, int TotalCount)> GetDecidedByRolesAsync(
        IEnumerable<string> roles, int page, int pageSize, CancellationToken ct = default);
}