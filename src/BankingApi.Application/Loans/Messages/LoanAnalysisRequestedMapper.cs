using BankingApi.Domain.Entities;

namespace BankingApi.Application.Loans.Messages;

/// <summary>
/// Maps domain loan entities to the LoanAnalysisRequested message contract.
/// Responsible for applying Phase 0 integration defaults cleanly and explicitly.
/// This is an integration adapter — not domain logic.
/// </summary>
public static class LoanAnalysisRequestedMapper
{
    /// <summary>
    /// Maps a loan at creation time — no documents yet (Phase 0 defaults).
    /// </summary>
    public static LoanAnalysisRequested Map(Loan loan) =>
        Map(loan, [], false);

    /// <summary>
    /// Maps a loan with real document references — used by RetryAiAnalysisHandler
    /// after documents have been uploaded.
    /// </summary>
    public static LoanAnalysisRequested Map(
        Loan loan,
        List<string> documentReferences,
        bool hasDocuments) => loan switch
    {
        PayrollLoan pl => MapPayroll(pl, documentReferences, hasDocuments),
        PersonalLoan _ => MapPersonal(loan, documentReferences, hasDocuments),
        _ => throw new InvalidOperationException($"Unknown loan type: {loan.GetType().Name}")
    };

    private static LoanAnalysisRequested MapPersonal(
        Loan loan,
        List<string> documentReferences,
        bool hasDocuments) => new(
        LoanApplicationId:  loan.Id,
        LoanType:           "Personal",
        RequestedAmount:    loan.Amount,
        TermMonths:         loan.Installments,
        ApplicantId:        loan.ClientId,
        DeclaredIncome:     null,              // Phase 0: not applicable for PersonalLoan
        EmploymentStatus:   "NotApplicable",   // Phase 0: not applicable for PersonalLoan
        HasDocuments:       hasDocuments,
        DocumentReferences: documentReferences,
        RequestedAt:        loan.RequestedAt);

    private static LoanAnalysisRequested MapPayroll(
        PayrollLoan loan,
        List<string> documentReferences,
        bool hasDocuments) => new(
        LoanApplicationId:  loan.Id,
        LoanType:           "Payroll",
        RequestedAmount:    loan.Amount,
        TermMonths:         loan.Installments,
        ApplicantId:        loan.ClientId,
        DeclaredIncome:     loan.MonthlySalary,
        EmploymentStatus:   loan.EmploymentStatus.ToString(),
        HasDocuments:       hasDocuments,
        DocumentReferences: documentReferences,
        RequestedAt:        loan.RequestedAt);
}