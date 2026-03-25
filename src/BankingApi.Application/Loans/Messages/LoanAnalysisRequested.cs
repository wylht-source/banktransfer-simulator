namespace BankingApi.Application.Loans.Messages;

/// <summary>
/// Event published to Azure Service Bus when a loan is requested.
/// Consumed asynchronously by the Python AI risk analysis service.
///
/// Phase 0 integration notes:
/// - declaredIncome is only populated for PayrollLoan (MonthlySalary).
///   PersonalLoan sends null — absence of data, not zero income.
/// - employmentStatus is "NotApplicable" for PersonalLoan.
/// - hasDocuments is always false — document support not yet implemented.
/// - documentReferences is always empty — Phase 0 placeholder.
/// These defaults are intentional and forward-compatible with future document analysis.
/// </summary>
public record LoanAnalysisRequested(
    Guid   LoanApplicationId,
    string LoanType,             // "Personal" | "Payroll"
    decimal RequestedAmount,
    int    TermMonths,
    string ApplicantId,
    decimal? DeclaredIncome,     // null for PersonalLoan, MonthlySalary for PayrollLoan
    string EmploymentStatus,     // "NotApplicable" for PersonalLoan
    bool   HasDocuments,         // always false in Phase 0
    List<string> DocumentReferences, // always empty in Phase 0
    DateTime RequestedAt);
