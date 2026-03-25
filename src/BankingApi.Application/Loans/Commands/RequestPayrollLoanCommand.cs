using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Messages;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;

namespace BankingApi.Application.Loans.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record RequestPayrollLoanCommand(
    string ClientId,
    decimal Amount,
    int Installments,
    string EmployerName,
    decimal MonthlySalary,
    EmploymentStatus EmploymentStatus,
    decimal ExistingPayrollDeductions);

// ── Result ───────────────────────────────────────────────────────────────────
public record RequestPayrollLoanResult(
    Guid LoanId,
    decimal Amount,
    int Installments,
    decimal InterestRate,
    decimal MonthlyPayment,
    string RequiredApprovalRole,
    LoanStatus Status,
    AiAnalysisStatus AiAnalysisStatus,
    DateTime RequestedAt,
    // Payroll summary
    decimal PayrollMarginLimit,
    decimal AvailablePayrollMargin,
    decimal RemainingPayrollMargin,
    decimal MarginUsageAfterApproval);

// ── Handler ──────────────────────────────────────────────────────────────────
public class RequestPayrollLoanHandler(
    ILoanRepository loanRepository,
    IMessagePublisher messagePublisher)
{
    private const string LoanAnalysisQueue = "loan-analysis-requests";

    public async Task<RequestPayrollLoanResult> Handle(
        RequestPayrollLoanCommand command, CancellationToken ct = default)
    {
        var loan = new PayrollLoan(
            clientId:                  command.ClientId,
            amount:                    command.Amount,
            installments:              command.Installments,
            employerName:              command.EmployerName,
            monthlySalary:             command.MonthlySalary,
            employmentStatus:          command.EmploymentStatus,
            existingPayrollDeductions: command.ExistingPayrollDeductions);

        await loanRepository.AddAsync(loan, ct);
        await loanRepository.SaveChangesAsync(ct);

        // Publish to Service Bus — non-blocking, AI enrichment must not block loan creation
        var message   = LoanAnalysisRequestedMapper.Map(loan);
        var published = await messagePublisher.PublishAsync(LoanAnalysisQueue, message, ct);

        loan.UpdateAiAnalysisStatus(published
            ? AiAnalysisStatus.Pending
            : AiAnalysisStatus.Failed);

        await loanRepository.SaveChangesAsync(ct);

        return new RequestPayrollLoanResult(
            LoanId:                   loan.Id,
            Amount:                   loan.Amount,
            Installments:             loan.Installments,
            InterestRate:             loan.InterestRate,
            MonthlyPayment:           loan.MonthlyPayment,
            RequiredApprovalRole:     loan.RequiredApprovalRole,
            Status:                   loan.Status,
            AiAnalysisStatus:         loan.AiAnalysisStatus,
            RequestedAt:              loan.RequestedAt,
            PayrollMarginLimit:       loan.PayrollMarginLimit,
            AvailablePayrollMargin:   loan.AvailablePayrollMargin,
            RemainingPayrollMargin:   loan.RemainingPayrollMargin,
            MarginUsageAfterApproval: loan.MarginUsageAfterApproval);
    }
}