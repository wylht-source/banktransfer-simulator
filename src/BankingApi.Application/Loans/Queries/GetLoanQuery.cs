using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────
public record LoanApprovalHistoryDto(
    string UserId,
    string Role,
    LoanDecision Decision,
    DateTime DecisionAt,
    string? Comment);

public record LoanDetailResult(
    Guid LoanId,
    string ClientId,
    decimal Amount,
    int Installments,
    decimal InterestRate,
    decimal MonthlyPayment,
    LoanStatus Status,
    string RequiredApprovalRole,
    DateTime RequestedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason,
    IEnumerable<LoanApprovalHistoryDto> ApprovalHistory);

// ── Query ─────────────────────────────────────────────────────────────────────
public record GetLoanQuery(
    Guid LoanId,
    string RequesterId,
    string RequesterRole);

// ── Handler ──────────────────────────────────────────────────────────────────
public class GetLoanHandler(ILoanRepository loanRepository)
{
    private static readonly string[] ApproverRoles =
    [
        Loan.RoleManager,
        Loan.RoleSupervisor,
        Loan.RoleCreditCommittee
    ];

    public async Task<LoanDetailResult> Handle(GetLoanQuery query, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdWithHistoryAsync(query.LoanId, ct)
            ?? throw new DomainException($"Loan '{query.LoanId}' not found.");

        // Client can only see their own loans.
        // Bank roles (Manager, Supervisor, CreditCommittee) can see any loan.
        var isApprover = ApproverRoles.Contains(query.RequesterRole);

        if (!isApprover && loan.ClientId != query.RequesterId)
            throw new DomainException("Access denied.");

        return MapToResult(loan);
    }

    private static LoanDetailResult MapToResult(Loan loan) => new(
        LoanId:              loan.Id,
        ClientId:            loan.ClientId,
        Amount:              loan.Amount,
        Installments:        loan.Installments,
        InterestRate:        loan.InterestRate,
        MonthlyPayment:      loan.MonthlyPayment,
        Status:              loan.Status,
        RequiredApprovalRole: loan.RequiredApprovalRole,
        RequestedAt:         loan.RequestedAt,
        ApprovedBy:          loan.ApprovedBy,
        ApprovedAt:          loan.ApprovedAt,
        RejectionReason:     loan.RejectionReason,
        ApprovalHistory:     loan.ApprovalHistory.Select(h => new LoanApprovalHistoryDto(
            h.UserId, h.Role, h.Decision, h.DecisionAt, h.Comment)));
}