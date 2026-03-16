using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Domain.Entities;

public class Loan
{
    // ── Approval thresholds ──────────────────────────────────────────────────
    private const decimal ManagerLimit    = 20_000m;
    private const decimal SupervisorLimit = 100_000m;

    // Role name constants — must match ASP.NET Identity seeds
    public const string RoleManager         = "Manager";
    public const string RoleSupervisor      = "Supervisor";
    public const string RoleCreditCommittee = "CreditCommittee";

    // ── Properties ───────────────────────────────────────────────────────────
    public Guid     Id                   { get; private set; }
    public string   ClientId             { get; private set; } = null!;
    public decimal  Amount               { get; private set; }
    public int      Installments         { get; private set; }
    public decimal  InterestRate         { get; private set; }   // monthly, e.g. 0.015
    public decimal  MonthlyPayment       { get; private set; }   // snapshot at creation
    public LoanStatus Status             { get; private set; }
    public string   RequiredApprovalRole { get; private set; } = null!;     // immutable after creation
    public DateTime RequestedAt          { get; private set; }
    public string?  ApprovedBy           { get; private set; }
    public DateTime? ApprovedAt          { get; private set; }
    public string?  RejectionReason      { get; private set; }

    private readonly List<LoanApprovalHistory> _approvalHistory = new();
    public IReadOnlyCollection<LoanApprovalHistory> ApprovalHistory => _approvalHistory.AsReadOnly();

    // ── EF Core constructor ──────────────────────────────────────────────────
    private Loan() { }

    // ── Factory / constructor ────────────────────────────────────────────────
    public Loan(string clientId, decimal amount, int installments, decimal interestRate)
    {
        ValidateCreation(amount, installments);

        Id                   = Guid.NewGuid();
        ClientId             = clientId;
        Amount               = amount;
        Installments         = installments;
        InterestRate         = interestRate;
        MonthlyPayment       = CalculatePmt(amount, interestRate, installments);
        Status               = LoanStatus.PendingApproval;
        RequiredApprovalRole = DetermineRequiredRole(amount);  // immutable from here
        RequestedAt          = DateTime.UtcNow;
    }

    // ── Domain behaviours ────────────────────────────────────────────────────

    public void Approve(string approverId, string approverRole)
    {
        EnsureCanTransition();
        EnsureHasAuthority(approverRole);

        Status     = LoanStatus.Approved;
        ApprovedBy = approverId;
        ApprovedAt = DateTime.UtcNow;

        _approvalHistory.Add(new LoanApprovalHistory(
            loanId:   Id,
            userId:   approverId,
            role:     approverRole,
            decision: LoanDecision.Approved,
            comment:  null));
    }

    public void Reject(string rejecterId, string rejecterRole, string reason)
    {
        EnsureCanTransition();
        EnsureHasAuthority(rejecterRole);

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A rejection reason is required.");

        Status          = LoanStatus.Rejected;
        RejectionReason = reason;

        _approvalHistory.Add(new LoanApprovalHistory(
            loanId:   Id,
            userId:   rejecterId,
            role:     rejecterRole,
            decision: LoanDecision.Rejected,
            comment:  reason));
    }

    public void Cancel(string clientId)
    {
        if (Status != LoanStatus.PendingApproval)
            throw new DomainException("Only pending loans can be cancelled.");

        if (ClientId != clientId)
            throw new DomainException("Only the loan owner can cancel it.");

        Status = LoanStatus.Cancelled;

        _approvalHistory.Add(new LoanApprovalHistory(
            loanId:   Id,
            userId:   clientId,
            role:     "Client",
            decision: LoanDecision.Cancelled,
            comment:  null));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void EnsureCanTransition()
    {
        if (Status != LoanStatus.PendingApproval)
            throw new DomainException($"Loan cannot be actioned in status '{Status}'.");
    }

    private void EnsureHasAuthority(string approverRole)
    {
        var approverLevel = GetRoleLevel(approverRole);
        var requiredLevel = GetRoleLevel(RequiredApprovalRole);

        if (approverLevel < requiredLevel)
            throw new DomainException(
                $"Role '{approverRole}' does not have authority to action this loan. " +
                $"Required: '{RequiredApprovalRole}'.");
    }

    // Hierarchical: higher roles can approve lower-level loans
    private static int GetRoleLevel(string role) => role switch
    {
        RoleManager         => 1,
        RoleSupervisor      => 2,
        RoleCreditCommittee => 3,
        _ => throw new DomainException($"Unknown approval role: '{role}'.")
    };

    private static string DetermineRequiredRole(decimal amount) => amount switch
    {
        <= ManagerLimit    => RoleManager,
        <= SupervisorLimit => RoleSupervisor,
        _                  => RoleCreditCommittee
    };

    /// <summary>
    /// Standard PMT formula: M = P * [r(1+r)^n] / [(1+r)^n - 1]
    /// </summary>
    private static decimal CalculatePmt(decimal principal, decimal monthlyRate, int n)
    {
        if (monthlyRate == 0)
            return Math.Round(principal / n, 2);

        var r = (double)monthlyRate;
        var factor = Math.Pow(1 + r, n);
        var pmt = (double)principal * (r * factor) / (factor - 1);
        return Math.Round((decimal)pmt, 2);
    }

    private static void ValidateCreation(decimal amount, int installments)
    {
        if (amount < 1_000m || amount > 200_000m)
            throw new DomainException("Loan amount must be between 1,000 and 200,000.");

        if (installments < 1 || installments > 48)
            throw new DomainException("Installments must be between 1 and 48.");
    }
}
