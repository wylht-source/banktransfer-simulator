using BankingApi.Domain.Entities;

namespace BankingApi.Application.Loans.Messages;

/// <summary>
/// Maps domain loan entities to the LoanAnalysisRequested message contract.
/// Responsible for applying Phase 0 integration defaults cleanly and explicitly.
/// This is an integration adapter — not domain logic.
/// </summary>
public static class LoanAnalysisRequestedMapper
{
    public static LoanAnalysisRequested Map(Loan loan) => loan switch
    {
        PayrollLoan pl => MapPayroll(pl),
        PersonalLoan _ => MapPersonal(loan),
        _ => throw new InvalidOperationException($"Unknown loan type: {loan.GetType().Name}")
    };

    private static LoanAnalysisRequested MapPersonal(Loan loan) => new(
        LoanApplicationId:    loan.Id,
        LoanType:             "Personal",
        RequestedAmount:      loan.Amount,
        TermMonths:           loan.Installments,
        ApplicantId:          loan.ClientId,
        DeclaredIncome:       null,              // Phase 0: not applicable for PersonalLoan
        EmploymentStatus:     "NotApplicable",   // Phase 0: not applicable for PersonalLoan
        HasDocuments:         false,             // Phase 0: document support not implemented
        DocumentReferences:   [],               // Phase 0: always empty
        RequestedAt:          loan.RequestedAt);

    private static LoanAnalysisRequested MapPayroll(PayrollLoan loan) => new(
        LoanApplicationId:    loan.Id,
        LoanType:             "Payroll",
        RequestedAmount:      loan.Amount,
        TermMonths:           loan.Installments,
        ApplicantId:          loan.ClientId,
        DeclaredIncome:       loan.MonthlySalary,
        EmploymentStatus:     loan.EmploymentStatus.ToString(),
        HasDocuments:         false,             // Phase 0: document support not implemented
        DocumentReferences:   [],               // Phase 0: always empty
        RequestedAt:          loan.RequestedAt);
}
