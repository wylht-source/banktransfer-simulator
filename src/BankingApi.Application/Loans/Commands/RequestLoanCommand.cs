using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Application.Interfaces;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record RequestLoanCommand(
    string ClientId,
    decimal Amount,
    int Installments);

// ── Result ───────────────────────────────────────────────────────────────────
public record RequestLoanResult(
    Guid LoanId,
    decimal Amount,
    int Installments,
    decimal InterestRate,
    decimal MonthlyPayment,
    string RequiredApprovalRole,
    LoanStatus Status,
    DateTime RequestedAt);

// ── Handler ──────────────────────────────────────────────────────────────────
public class RequestLoanHandler(ILoanRepository loanRepository)
{
    // In a real system this would come from a product/rate configuration service.
    private const decimal MonthlyInterestRate = 0.015m;

    public async Task<RequestLoanResult> Handle(RequestLoanCommand command, CancellationToken ct = default)
    {
        var loan = new PersonalLoan(
            clientId:     command.ClientId,
            amount:       command.Amount,
            installments: command.Installments);

        await loanRepository.AddAsync(loan, ct);
        await loanRepository.SaveChangesAsync(ct);

        return new RequestLoanResult(
            LoanId:              loan.Id,
            Amount:              loan.Amount,
            Installments:        loan.Installments,
            InterestRate:        loan.InterestRate,
            MonthlyPayment:      loan.MonthlyPayment,
            RequiredApprovalRole: loan.RequiredApprovalRole,
            Status:              loan.Status,
            RequestedAt:         loan.RequestedAt);
    }
}