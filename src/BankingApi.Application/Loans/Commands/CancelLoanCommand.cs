using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record CancelLoanCommand(
    Guid LoanId,
    string ClientId);

// ── Result ───────────────────────────────────────────────────────────────────
public record CancelLoanResult(Guid LoanId);

// ── Handler ──────────────────────────────────────────────────────────────────
public class CancelLoanHandler(ILoanRepository loanRepository, ILogger<CancelLoanHandler> logger)
{
    public async Task<CancelLoanResult> Handle(CancelLoanCommand command, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        // Ownership + status check delegated to domain entity
        loan.Cancel(command.ClientId);

        await loanRepository.SaveChangesAsync(ct);
         logger.LogInformation(
            "LoanCancelled — LoanId: {LoanId}, ClientId: {ClientId}, Amount: {Amount}",
            loan.Id, loan.ClientId, loan.Amount);

        return new CancelLoanResult(loan.Id);
    }
}