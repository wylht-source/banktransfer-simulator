using BankingApi.Domain.Enums;

namespace BankingApi.Domain.Entities;

public class LoanApprovalHistory
{
    public Guid Id { get; private set; }
    public Guid LoanId { get; private set; }
    public string UserId { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public LoanDecision Decision { get; private set; }
    public DateTime DecisionAt { get; private set; }
    public string? Comment { get; private set; }

    // EF Core constructor
    private LoanApprovalHistory() { }

    internal LoanApprovalHistory(
        Guid loanId,
        string userId,
        string role,
        LoanDecision decision,
        string? comment)
    {
        Id = Guid.NewGuid();
        LoanId = loanId;
        UserId = userId;
        Role = role;
        Decision = decision;
        DecisionAt = DateTime.UtcNow;
        Comment = comment;
    }
}
