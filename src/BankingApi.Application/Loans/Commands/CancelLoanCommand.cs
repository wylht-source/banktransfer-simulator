using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record CancelLoanCommand(
    Guid LoanId,
    string ClientId);

// ── Result ───────────────────────────────────────────────────────────────────
public record CancelLoanResult(Guid LoanId);

// ── Handler ──────────────────────────────────────────────────────────────────
public class CancelLoanHandler(ILoanRepository loanRepository)
{
    public async Task<CancelLoanResult> Handle(CancelLoanCommand command, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        // Ownership + status check delegated to domain entity
        loan.Cancel(command.ClientId);

        await loanRepository.SaveChangesAsync(ct);

        return new CancelLoanResult(loan.Id);
    }
}