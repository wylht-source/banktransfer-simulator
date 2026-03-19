using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class LoanDomainTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────
    private static Loan CreateLoan(decimal amount = 5_000m, int installments = 12)
        => new("client-1", amount, installments, 0.015m);

    // ── Creation ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateLoan_ValidData_SetsCorrectRequiredRole()
    {
        var managerLoan     = CreateLoan(amount: 10_000m);
        var supervisorLoan  = CreateLoan(amount: 50_000m);
        var committeeLoan   = CreateLoan(amount: 150_000m);

        managerLoan.RequiredApprovalRole.Should().Be(Loan.RoleManager);
        supervisorLoan.RequiredApprovalRole.Should().Be(Loan.RoleSupervisor);
        committeeLoan.RequiredApprovalRole.Should().Be(Loan.RoleCreditCommittee);
    }

    


    [Fact]
    public void CreateLoan_ValidData_CalculatesMonthlyPayment()
    {
        var loan = CreateLoan(amount: 10_000m, installments: 12);

        // PMT formula: M = P * [r(1+r)^n] / [(1+r)^n - 1]
        // Expected ≈ 912.07
        loan.MonthlyPayment.Should().BeGreaterThan(0);
        loan.MonthlyPayment.Should().BeApproximately(916.80m, 0.01m);
    }

    [Fact]
    public void CreateLoan_ValidData_StatusIsPendingApproval()
    {
        var loan = CreateLoan();

        loan.Status.Should().Be(LoanStatus.PendingApproval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(200_001)]
    public void CreateLoan_InvalidAmount_ThrowsDomainException(decimal amount)
    {
        var act = () => new Loan("client-1", amount, 12, 0.015m);

        act.Should().Throw<DomainException>().WithMessage("*amount*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    public void CreateLoan_InvalidInstallments_ThrowsDomainException(int installments)
    {
        var act = () => new Loan("client-1", 5_000m, installments, 0.015m);

        act.Should().Throw<DomainException>().WithMessage("*nstallment*");
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_ByRequiredRole_ChangesStatusToApproved()
    {
        var loan = CreateLoan(amount: 10_000m); // RequiredRole = Manager

        loan.Approve("manager-1", Loan.RoleManager);

        loan.Status.Should().Be(LoanStatus.Approved);
        loan.ApprovedBy.Should().Be("manager-1");
        loan.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_BySuperiorRole_Succeeds()
    {
        var loan = CreateLoan(amount: 10_000m); // RequiredRole = Manager

        // Supervisor has authority over Manager-level loans
        loan.Approve("supervisor-1", Loan.RoleSupervisor);

        loan.Status.Should().Be(LoanStatus.Approved);
    }

    [Fact]
    public void Approve_ByInsufficientRole_ThrowsDomainException()
    {
        var loan = CreateLoan(amount: 50_000m); // RequiredRole = Supervisor

        var act = () => loan.Approve("manager-1", Loan.RoleManager);

        act.Should().Throw<DomainException>().WithMessage("*authority*");
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsDomainException()
    {
        var loan = CreateLoan(amount: 10_000m);
        loan.Approve("manager-1", Loan.RoleManager);

        var act = () => loan.Approve("manager-1", Loan.RoleManager);

        act.Should().Throw<DomainException>().WithMessage("*status*");
    }

    [Fact]
    public void Approve_AddsEntryToApprovalHistory()
    {
        var loan = CreateLoan(amount: 10_000m);

        loan.Approve("manager-1", Loan.RoleManager);

        loan.ApprovalHistory.Should().HaveCount(1);
        loan.ApprovalHistory.First().Decision.Should().Be(LoanDecision.Approved);
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_WithReason_ChangesStatusToRejected()
    {
        var loan = CreateLoan(amount: 10_000m);

        loan.Reject("manager-1", Loan.RoleManager, "Insufficient credit history.");

        loan.Status.Should().Be(LoanStatus.Rejected);
        loan.RejectionReason.Should().Be("Insufficient credit history.");
    }

    [Fact]
    public void Reject_WithoutReason_ThrowsDomainException()
    {
        var loan = CreateLoan(amount: 10_000m);

        var act = () => loan.Reject("manager-1", Loan.RoleManager, "");

        act.Should().Throw<DomainException>().WithMessage("*reason*");
    }

    [Fact]
    public void Reject_ByInsufficientRole_ThrowsDomainException()
    {
        var loan = CreateLoan(amount: 50_000m); // RequiredRole = Supervisor

        var act = () => loan.Reject("manager-1", Loan.RoleManager, "Too risky.");

        act.Should().Throw<DomainException>().WithMessage("*authority*");
    }

    [Fact]
    public void Reject_AddsEntryToApprovalHistory()
    {
        var loan = CreateLoan(amount: 10_000m);

        loan.Reject("manager-1", Loan.RoleManager, "Too risky.");

        loan.ApprovalHistory.Should().HaveCount(1);
        loan.ApprovalHistory.First().Decision.Should().Be(LoanDecision.Rejected);
        loan.ApprovalHistory.First().Comment.Should().Be("Too risky.");
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_ByOwner_ChangesStatusToCancelled()
    {
        var loan = CreateLoan();

        loan.Cancel("client-1");

        loan.Status.Should().Be(LoanStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ByWrongClient_ThrowsDomainException()
    {
        var loan = CreateLoan();

        var act = () => loan.Cancel("client-999");

        act.Should().Throw<DomainException>().WithMessage("*owner*");
    }

    [Fact]
    public void Cancel_AlreadyApproved_ThrowsDomainException()
    {
        var loan = CreateLoan(amount: 10_000m);
        loan.Approve("manager-1", Loan.RoleManager);

        var act = () => loan.Cancel("client-1");

        act.Should().Throw<DomainException>().WithMessage("*pending*");
    }
}
