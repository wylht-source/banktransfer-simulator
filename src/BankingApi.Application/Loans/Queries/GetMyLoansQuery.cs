using BankingApi.Application.Common;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Enums;

namespace BankingApi.Application.Loans.Queries;

// ── DTO ───────────────────────────────────────────────────────────────────────
public record LoanSummaryResult(
    Guid LoanId,
    decimal Amount,
    int Installments,
    decimal MonthlyPayment,
    LoanStatus Status,
    string RequiredApprovalRole,
    DateTime RequestedAt);

// ── Query ─────────────────────────────────────────────────────────────────────
public record GetMyLoansQuery(
    string ClientId,
    int Page,
    int PageSize);

// ── Handler ──────────────────────────────────────────────────────────────────
public class GetMyLoansHandler(ILoanRepository loanRepository)
{
    public async Task<PagedResult<LoanSummaryResult>> Handle(GetMyLoansQuery query, CancellationToken ct = default)
    {
        var (loans, totalCount) = await loanRepository.GetByClientIdAsync(
            query.ClientId,
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
}