using BankingApi.Application.Common;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;

namespace BankingApi.Application.Loans.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
public record GetDecidedLoansQuery(
    string ApproverRole,
    int Page,
    int PageSize);

// ── Handler ──────────────────────────────────────────────────────────────────
public class GetDecidedLoansHandler(ILoanRepository loanRepository)
{
    public async Task<PagedResult<LoanSummaryResult>> Handle(
        GetDecidedLoansQuery query, CancellationToken ct = default)
    {
        var actionableRoles = GetActionableRoles(query.ApproverRole);

        var (loans, totalCount) = await loanRepository.GetDecidedByRolesAsync(
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
            RequestedAt:         l.RequestedAt,
            LoanType:            l is PayrollLoan ? "Payroll" : "Personal"));

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
