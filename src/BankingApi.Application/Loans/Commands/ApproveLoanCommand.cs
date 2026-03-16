using BankingApi.Application.Interfaces;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record ApproveLoanCommand(
    Guid LoanId,
    string ApproverId,
    string ApproverRole);

// ── Result ───────────────────────────────────────────────────────────────────
public record ApproveLoanResult(
    Guid LoanId,
    string ApprovedBy,
    DateTime ApprovedAt);

// ── Handler ──────────────────────────────────────────────────────────────────
public class ApproveLoanHandler(ILoanRepository loanRepository)
{
    public async Task<ApproveLoanResult> Handle(ApproveLoanCommand command, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        // Authority check + state transition delegated to the domain entity
        loan.Approve(command.ApproverId, command.ApproverRole);

        await loanRepository.SaveChangesAsync(ct);

        return new ApproveLoanResult(
            LoanId:     loan.Id,
            ApprovedBy: loan.ApprovedBy!,
            ApprovedAt: loan.ApprovedAt!.Value);
    }
}