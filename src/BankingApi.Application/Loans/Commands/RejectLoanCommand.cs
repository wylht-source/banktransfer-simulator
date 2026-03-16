using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record RejectLoanCommand(
    Guid LoanId,
    string RejecterId,
    string RejecterRole,
    string Reason);           // mandatory — enforced in domain

// ── Result ───────────────────────────────────────────────────────────────────
public record RejectLoanResult(
    Guid LoanId,
    string RejectionReason);

// ── Handler ──────────────────────────────────────────────────────────────────
public class RejectLoanHandler(ILoanRepository loanRepository)
{
    public async Task<RejectLoanResult> Handle(RejectLoanCommand command, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        // Reason validation + authority check + state transition in domain entity
        loan.Reject(command.RejecterId, command.RejecterRole, command.Reason);

        await loanRepository.SaveChangesAsync(ct);

        return new RejectLoanResult(
            LoanId:          loan.Id,
            RejectionReason: loan.RejectionReason!);
    }
}