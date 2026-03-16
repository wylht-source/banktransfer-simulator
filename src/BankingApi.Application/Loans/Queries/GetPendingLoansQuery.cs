using BankingApi.Application.Common;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;

namespace BankingApi.Application.Loans.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
public record GetPendingLoansQuery(
    string ApproverRole,
    int Page,
    int PageSize);

// ── Handler ──────────────────────────────────────────────────────────────────
public class GetPendingLoansHandler(ILoanRepository loanRepository)
{
    public async Task<PagedResult<LoanSummaryResult>> Handle(GetPendingLoansQuery query, CancellationToken ct = default)
    {
        // Hierarchical: approver sees all loans they have authority to action.
        // Manager sees: Manager
        // Supervisor sees: Manager + Supervisor
        // CreditCommittee sees: Manager + Supervisor + CreditCommittee
        var actionableRoles = GetActionableRoles(query.ApproverRole);

        var (loans, totalCount) = await loanRepository.GetPendingByRolesAsync(
            actionableRoles,
            query.Page,
            query.PageSize,
            ct);

        var items = loans.Select(l => new LoanSummaryResult(
            LoanId:              l.Id,
            Amount:              l.Amount,
            Installments:        l.Installments,
            MonthlyPayment:      l.MonthlyPayment,
            Status:              l.Status,
            RequiredApprovalRole: l.RequiredApprovalRole,
            RequestedAt:         l.RequestedAt));

        return new PagedResult<LoanSummaryResult>(items, query.Page, query.PageSize, totalCount);
    }

    private static IEnumerable<string> GetActionableRoles(string approverRole) =>
        approverRole switch
        {
            Loan.RoleManager         => [Loan.RoleManager],
            Loan.RoleSupervisor      => [Loan.RoleManager, Loan.RoleSupervisor],
            Loan.RoleCreditCommittee => [Loan.RoleManager, Loan.RoleSupervisor, Loan.RoleCreditCommittee],
            _                        => []
        };
}