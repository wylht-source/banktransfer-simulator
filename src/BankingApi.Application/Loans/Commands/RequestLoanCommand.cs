using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Messages;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;

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
    AiAnalysisStatus AiAnalysisStatus,
    DateTime RequestedAt);

// ── Handler ──────────────────────────────────────────────────────────────────
public class RequestLoanHandler(
    ILoanRepository loanRepository,
    IMessagePublisher messagePublisher)
{
    private const string LoanAnalysisQueue = "loan-analysis-requests";

    public async Task<RequestLoanResult> Handle(RequestLoanCommand command, CancellationToken ct = default)
    {
        var loan = new PersonalLoan(
            clientId:     command.ClientId,
            amount:       command.Amount,
            installments: command.Installments);

        await loanRepository.AddAsync(loan, ct);
        await loanRepository.SaveChangesAsync(ct);

        // Publish to Service Bus — non-blocking, AI enrichment must not block loan creation
        var message   = LoanAnalysisRequestedMapper.Map(loan);
        var published = await messagePublisher.PublishAsync(LoanAnalysisQueue, message, ct);

        loan.UpdateAiAnalysisStatus(published
            ? AiAnalysisStatus.Pending
            : AiAnalysisStatus.Failed);

        await loanRepository.SaveChangesAsync(ct);

        return new RequestLoanResult(
            LoanId:              loan.Id,
            Amount:              loan.Amount,
            Installments:        loan.Installments,
            InterestRate:        loan.InterestRate,
            MonthlyPayment:      loan.MonthlyPayment,
            RequiredApprovalRole: loan.RequiredApprovalRole,
            Status:              loan.Status,
            AiAnalysisStatus:    loan.AiAnalysisStatus,
            RequestedAt:         loan.RequestedAt);
    }
}