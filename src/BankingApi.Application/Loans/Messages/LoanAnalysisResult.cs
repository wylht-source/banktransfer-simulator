namespace BankingApi.Application.Loans.Messages;

/// <summary>
/// Schema for the result returned by the Python AI risk analysis service.
/// Not yet consumed — defined here for contract clarity and future implementation.
/// </summary>
public record LoanAnalysisResult(
    Guid   LoanApplicationId,
    double RiskScore,            // 0.0 (lowest risk) to 1.0 (highest risk)
    string RiskLevel,            // "Low" | "Medium" | "High"
    string RecommendedAction,    // "approve" | "manual_review" | "reject"
    DocumentAnalysisResult? DocumentAnalysis,
    DateTime ProcessedAt);

/// <summary>
/// Populated only when documents were submitted and analyzed.
/// Null in Phase 0.
/// </summary>
public record DocumentAnalysisResult(
    double ConfidenceScore,
    List<string> Findings,
    Dictionary<string, string> ExtractedData);
