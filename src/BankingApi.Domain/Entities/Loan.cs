using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Domain.Entities;

public abstract class Loan
{
    // ── Approval thresholds ──────────────────────────────────────────────────
    public const decimal ManagerLimit    = 20_000m;
    public const decimal SupervisorLimit = 100_000m;

    // Role name constants — must match ASP.NET Identity seeds
    public const string RoleManager         = "Manager";
    public const string RoleSupervisor      = "Supervisor";
    public const string RoleCreditCommittee = "CreditCommittee";

    // ── Common properties ────────────────────────────────────────────────────
    public Guid      Id                   { get; protected set; }
    public string    ClientId             { get; protected set; } = null!;
    public decimal   Amount               { get; protected set; }
    public int       Installments         { get; protected set; }
    public decimal   InterestRate         { get; protected set; }
    public decimal   MonthlyPayment       { get; protected set; }
    public LoanStatus Status              { get; protected set; }
    public string    RequiredApprovalRole { get; protected set; } = null!;
    public DateTime  RequestedAt          { get; protected set; }
    public string?   ApprovedBy           { get; protected set; }
    public DateTime? ApprovedAt           { get; protected set; }
    public string?   RejectionReason      { get; protected set; }

    private readonly List<LoanApprovalHistory> _approvalHistory = new();
    public IReadOnlyCollection<LoanApprovalHistory> ApprovalHistory => _approvalHistory.AsReadOnly();

    // ── EF Core constructor ──────────────────────────────────────────────────
    protected Loan() { }

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

    // ── Protected helpers ────────────────────────────────────────────────────

    protected void InitializeCommonFields(
        string clientId, decimal amount, int installments,
        decimal interestRate, string requiredApprovalRole)
    {
        Id                   = Guid.NewGuid();
        ClientId             = clientId;
        Amount               = amount;
        Installments         = installments;
        InterestRate         = interestRate;
        MonthlyPayment       = CalculatePmt(amount, interestRate, installments);
        Status               = LoanStatus.PendingApproval;
        RequiredApprovalRole = requiredApprovalRole;
        RequestedAt          = DateTime.UtcNow;
    }

    protected static string DetermineRequiredRole(decimal amount, decimal supervisorLimit) =>
        amount switch
        {
            <= ManagerLimit                 => RoleManager,
            var a when a <= supervisorLimit => RoleSupervisor,
            _                               => RoleCreditCommittee
        };

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

    private static int GetRoleLevel(string role) => role switch
    {
        RoleManager         => 1,
        RoleSupervisor      => 2,
        RoleCreditCommittee => 3,
        _ => throw new DomainException($"Unknown approval role: '{role}'.")
    };

    /// <summary>Standard PMT formula: M = P * [r(1+r)^n] / [(1+r)^n - 1]</summary>
    protected static decimal CalculatePmt(decimal principal, decimal monthlyRate, int n)
    {
        if (monthlyRate == 0)
            return Math.Round(principal / n, 2);

        var r      = (double)monthlyRate;
        var factor = Math.Pow(1 + r, n);
        var pmt    = (double)principal * (r * factor) / (factor - 1);
        return Math.Round((decimal)pmt, 2);
    }
}