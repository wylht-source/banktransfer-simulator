using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Messages;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record RetryAiAnalysisCommand(Guid LoanId);

// ── Result ───────────────────────────────────────────────────────────────────
public record RetryAiAnalysisResult(
    Guid LoanId,
    AiAnalysisStatus AiAnalysisStatus);

// ── Handler ──────────────────────────────────────────────────────────────────
public class RetryAiAnalysisHandler(
    ILoanRepository loanRepository,
    IMessagePublisher messagePublisher)
{
    private const string LoanAnalysisQueue = "loan-analysis-requests";

    public async Task<RetryAiAnalysisResult> Handle(
        RetryAiAnalysisCommand command, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        if (loan.AiAnalysisStatus == AiAnalysisStatus.Completed)
            throw new DomainException("AI analysis is already completed for this loan.");

        var message   = LoanAnalysisRequestedMapper.Map(loan);
        var published = await messagePublisher.PublishAsync(LoanAnalysisQueue, message, ct);

        loan.UpdateAiAnalysisStatus(published
            ? AiAnalysisStatus.Pending
            : AiAnalysisStatus.Failed);

        await loanRepository.SaveChangesAsync(ct);

        return new RetryAiAnalysisResult(
            LoanId:           loan.Id,
            AiAnalysisStatus: loan.AiAnalysisStatus);
    }
}