using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Services;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.Loans.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record LoanSummaryDto(
    Guid LoanId,
    string ClientId,
    string ClientDisplayName,
    string LoanType,
    decimal Amount,
    int Installments,
    decimal InterestRateMonthly,
    decimal MonthlyPayment,
    DateTime RequestedAt,
    string RequiredApprovalRole,
    AiAnalysisStatus AiAnalysisStatus,
    LoanStatus Status);

public record PaymentScheduleItemDto(
    int InstallmentNumber,
    DateTime DueDate,
    decimal PaymentAmount);

public record CustomerPaymentViewDto(
    decimal MonthlyPayment,
    decimal TotalPayable,
    decimal TotalInterestCharged,
    DateTime FirstDueDate,
    DateTime LastDueDate,
    bool IsEstimated,
    IEnumerable<PaymentScheduleItemDto> PaymentSchedule);

public record BankProfitabilityViewDto(
    decimal TotalPayable,
    decimal GrossInterestRevenue,
    decimal EstimatedFundingCost,
    decimal ExpectedCreditLoss,
    decimal EstimatedOperationalCost,
    decimal EstimatedCapitalCharge,
    decimal EstimatedNetProfit,
    decimal EstimatedProfitMargin);

public record PayrollSummaryDto(
    string EmployerName,
    decimal MonthlySalary,
    EmploymentStatus EmploymentStatus,
    decimal ExistingPayrollDeductions,
    decimal PayrollMarginLimit,
    decimal AvailablePayrollMargin,
    decimal MonthlyPayment,
    decimal RemainingPayrollMargin,
    decimal MarginUsageAfterApproval);

public record WorkflowHistoryItemDto(
    string Action,
    string PerformedBy,
    string PerformedByRole,
    DateTime PerformedAt,
    string? Comment);

public record LoanApprovalDetailsResult(
    LoanSummaryDto LoanSummary,
    CustomerPaymentViewDto CustomerPaymentView,
    BankProfitabilityViewDto BankProfitabilityView,
    PayrollSummaryDto? PayrollSummary,      // null for PersonalLoan
    IEnumerable<WorkflowHistoryItemDto> WorkflowHistory);

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetLoanApprovalDetailsQuery(
    Guid LoanId,
    string RequesterId,
    string RequesterRole);

// ── Handler ──────────────────────────────────────────────────────────────────

public class GetLoanApprovalDetailsHandler(
    ILoanRepository loanRepository,
    LoanProfitabilityService profitabilityService,
    IIdentityService identityService)
{
    private static readonly string[] ApproverRoles =
    [
        Loan.RoleManager,
        Loan.RoleSupervisor,
        Loan.RoleCreditCommittee
    ];

    public async Task<LoanApprovalDetailsResult> Handle(
        GetLoanApprovalDetailsQuery query, CancellationToken ct = default)
    {
        if (!ApproverRoles.Contains(query.RequesterRole))
            throw new DomainException("Access denied. Approval details are restricted to bank roles.");

        var loan = await loanRepository.GetByIdWithHistoryAsync(query.LoanId, ct)
            ?? throw new DomainException($"Loan '{query.LoanId}' not found.");

        var clientDisplayName = await ResolveClientDisplayName(loan.ClientId);
        var profitability     = profitabilityService.Calculate(loan);
        var paymentView       = BuildCustomerPaymentView(loan);
        var workflowHistory   = BuildWorkflowHistory(loan);
        var loanType          = loan is PayrollLoan ? "Payroll" : "Personal";
        var payrollSummary    = loan is PayrollLoan pl ? BuildPayrollSummary(pl) : null;

        return new LoanApprovalDetailsResult(
            LoanSummary: new LoanSummaryDto(
                LoanId:              loan.Id,
                ClientId:            loan.ClientId,
                ClientDisplayName:   clientDisplayName,
                LoanType:            loanType,
                Amount:              loan.Amount,
                Installments:        loan.Installments,
                InterestRateMonthly: loan.InterestRate,
                MonthlyPayment:      loan.MonthlyPayment,
                RequestedAt:         loan.RequestedAt,
                RequiredApprovalRole: loan.RequiredApprovalRole,
                AiAnalysisStatus: loan.AiAnalysisStatus,
                Status:              loan.Status),

            CustomerPaymentView: paymentView,

            BankProfitabilityView: new BankProfitabilityViewDto(
                TotalPayable:             profitability.TotalPayable,
                GrossInterestRevenue:     profitability.GrossInterestRevenue,
                EstimatedFundingCost:     profitability.EstimatedFundingCost,
                ExpectedCreditLoss:       profitability.ExpectedCreditLoss,
                EstimatedOperationalCost: profitability.EstimatedOperationalCost,
                EstimatedCapitalCharge:   profitability.EstimatedCapitalCharge,
                EstimatedNetProfit:       profitability.EstimatedNetProfit,
                EstimatedProfitMargin:    profitability.EstimatedProfitMargin),

            PayrollSummary: payrollSummary,
            WorkflowHistory: workflowHistory);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> ResolveClientDisplayName(string clientId)
    {
        var name = await identityService.GetDisplayNameAsync(clientId);
        return name ?? clientId;
    }

    private static CustomerPaymentViewDto BuildCustomerPaymentView(Loan loan)
    {
        var isEstimated = loan.Status == LoanStatus.PendingApproval;
        var anchor      = isEstimated ? DateTime.UtcNow : loan.ApprovedAt!.Value;
        var firstDue    = anchor.AddDays(30);

        var schedule = Enumerable.Range(1, loan.Installments)
            .Select(n => new PaymentScheduleItemDto(
                InstallmentNumber: n,
                DueDate:           firstDue.AddMonths(n - 1),
                PaymentAmount:     loan.MonthlyPayment))
            .ToList();

        return new CustomerPaymentViewDto(
            MonthlyPayment:       loan.MonthlyPayment,
            TotalPayable:         loan.MonthlyPayment * loan.Installments,
            TotalInterestCharged: (loan.MonthlyPayment * loan.Installments) - loan.Amount,
            FirstDueDate:         firstDue,
            LastDueDate:          firstDue.AddMonths(loan.Installments - 1),
            IsEstimated:          isEstimated,
            PaymentSchedule:      schedule);
    }

    private static PayrollSummaryDto BuildPayrollSummary(PayrollLoan loan) => new(
        EmployerName:              loan.EmployerName,
        MonthlySalary:             loan.MonthlySalary,
        EmploymentStatus:          loan.EmploymentStatus,
        ExistingPayrollDeductions: loan.ExistingPayrollDeductions,
        PayrollMarginLimit:        loan.PayrollMarginLimit,
        AvailablePayrollMargin:    loan.AvailablePayrollMargin,
        MonthlyPayment:            loan.MonthlyPayment,
        RemainingPayrollMargin:    loan.RemainingPayrollMargin,
        MarginUsageAfterApproval:  loan.MarginUsageAfterApproval);

    private static IEnumerable<WorkflowHistoryItemDto> BuildWorkflowHistory(Loan loan) =>
        loan.ApprovalHistory
            .OrderBy(h => h.DecisionAt)
            .Select(h => new WorkflowHistoryItemDto(
                Action:          h.Decision.ToString(),
                PerformedBy:     h.UserId,
                PerformedByRole: h.Role,
                PerformedAt:     h.DecisionAt,
                Comment:         h.Comment));
}