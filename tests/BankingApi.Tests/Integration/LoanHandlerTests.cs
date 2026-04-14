using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Loans.Queries;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using BankingApi.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankingApi.Tests.Integration;

public class LoanHandlerTests
{
    private readonly BankingApi.Infrastructure.Persistence.BankingDbContext _db;
    private readonly LoanRepository _loanRepo;
    private readonly RequestLoanHandler _requestHandler;
    private readonly ApproveLoanHandler _approveHandler;
    private readonly RejectLoanHandler _rejectHandler;
    private readonly CancelLoanHandler _cancelHandler;
    private readonly GetLoanHandler _getLoanHandler;
    private readonly GetMyLoansHandler _getMyLoansHandler;
    private readonly GetPendingLoansHandler _getPendingHandler;

    private const string ClientId   = "client-integration-1";
    private const string ManagerId  = "manager-integration-1";
    private const string SupervisorId = "supervisor-integration-1";

    public LoanHandlerTests()
    {
        _db = BankingDbContextFactory.Create();

        var logger = NullLogger<LoanRepository>.Instance;
        _loanRepo = new LoanRepository(_db, logger);

        var nullMessagePublisher = new BankingApi.Infrastructure.Services.Messaging.NullMessagePublisher(
            NullLogger<BankingApi.Infrastructure.Services.Messaging.NullMessagePublisher>.Instance);

        _requestHandler    = new RequestLoanHandler(_loanRepo, nullMessagePublisher, NullLogger<RequestLoanHandler>.Instance);
        _approveHandler    = new ApproveLoanHandler(_loanRepo);
        _rejectHandler     = new RejectLoanHandler(_loanRepo);
        _cancelHandler     = new CancelLoanHandler(_loanRepo, NullLogger<CancelLoanHandler>.Instance);
        _getLoanHandler    = new GetLoanHandler(_loanRepo);
        _getMyLoansHandler = new GetMyLoansHandler(_loanRepo);
        _getPendingHandler = new GetPendingLoansHandler(_loanRepo);
    }

    // ── Request ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestLoan_ValidData_ReturnsLoanWithPendingStatus()
    {
        var result = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));

        result.LoanId.Should().NotBeEmpty();
        result.Status.Should().Be(LoanStatus.PendingApproval);
        result.RequiredApprovalRole.Should().Be("Manager");
        result.MonthlyPayment.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RequestLoan_InvalidAmount_ThrowsDomainException()
    {
        var act = async () => await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 500m, 12, Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*amount*");
    }

    [Fact]
    public async Task RequestLoan_SameIdempotencyKey_ReturnsExistingLoan()
    {
        var idempotencyKey = Guid.NewGuid();
        
        var result1 = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, idempotencyKey));

        var result2 = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, idempotencyKey));

        result1.LoanId.Should().Be(result2.LoanId);
        result1.Amount.Should().Be(result2.Amount);
        result1.Installments.Should().Be(result2.Installments);

        var allLoans = await _getMyLoansHandler.Handle(new GetMyLoansQuery(ClientId, 1, 10));
        allLoans.TotalCount.Should().Be(1);
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveLoan_ByAuthorizedRole_ChangesStatusToApproved()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid())); // Manager level

        var result = await _approveHandler.Handle(
            new ApproveLoanCommand(loan.LoanId, ManagerId, "Manager"));

        result.ApprovedBy.Should().Be(ManagerId);

        var detail = await _getLoanHandler.Handle(
            new GetLoanQuery(loan.LoanId, ManagerId, "Manager"));

        detail.Status.Should().Be(LoanStatus.Approved);
        detail.ApprovalHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApproveLoan_ByInsufficientRole_ThrowsDomainException()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 50_000m, 24, Guid.NewGuid())); // Supervisor level

        var act = async () => await _approveHandler.Handle(
            new ApproveLoanCommand(loan.LoanId, ManagerId, "Manager"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*authority*");
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectLoan_WithReason_ChangesStatusToRejected()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));

        var result = await _rejectHandler.Handle(
            new RejectLoanCommand(loan.LoanId, ManagerId, "Manager", "Insufficient credit history."));

        result.RejectionReason.Should().Be("Insufficient credit history.");

        var detail = await _getLoanHandler.Handle(
            new GetLoanQuery(loan.LoanId, ManagerId, "Manager"));

        detail.Status.Should().Be(LoanStatus.Rejected);
        detail.ApprovalHistory.Should().HaveCount(1);
        detail.ApprovalHistory.First().Comment.Should().Be("Insufficient credit history.");
    }

    [Fact]
    public async Task RejectLoan_WithoutReason_ThrowsDomainException()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));

        var act = async () => await _rejectHandler.Handle(
            new RejectLoanCommand(loan.LoanId, ManagerId, "Manager", ""));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reason*");
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelLoan_ByOwner_ChangesStatusToCancelled()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));

        await _cancelHandler.Handle(new CancelLoanCommand(loan.LoanId, ClientId));

        var detail = await _getLoanHandler.Handle(
            new GetLoanQuery(loan.LoanId, ClientId, "Client"));

        detail.Status.Should().Be(LoanStatus.Cancelled);
    }

    [Fact]
    public async Task CancelLoan_ByWrongClient_ThrowsDomainException()
    {
        var loan = await _requestHandler.Handle(
            new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));

        var act = async () => await _cancelHandler.Handle(
            new CancelLoanCommand(loan.LoanId, "wrong-client"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*owner*");
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyLoans_ReturnsPaginatedResults()
    {
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 10_000m, 24, Guid.NewGuid()));
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 15_000m, 36, Guid.NewGuid()));

        var result = await _getMyLoansHandler.Handle(
            new GetMyLoansQuery(ClientId, Page: 1, PageSize: 2));

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task GetPendingLoans_ManagerSeesOnlyManagerLevel()
    {
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));    // Manager
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 50_000m, 24, Guid.NewGuid()));   // Supervisor
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 150_000m, 48, Guid.NewGuid()));  // CreditCommittee

        var result = await _getPendingHandler.Handle(
            new GetPendingLoansQuery("Manager", Page: 1, PageSize: 10));

        result.Items.Should().HaveCount(1);
        result.Items.First().RequiredApprovalRole.Should().Be("Manager");
    }

    [Fact]
    public async Task GetPendingLoans_SupervisorSeesManagerAndSupervisorLevel()
    {
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 5_000m, 12, Guid.NewGuid()));    // Manager
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 50_000m, 24, Guid.NewGuid()));   // Supervisor
        await _requestHandler.Handle(new RequestLoanCommand(ClientId, 150_000m, 48, Guid.NewGuid()));  // CreditCommittee

        var result = await _getPendingHandler.Handle(
            new GetPendingLoansQuery("Supervisor", Page: 1, PageSize: 10));

        result.Items.Should().HaveCount(2);
        result.Items.Select(l => l.RequiredApprovalRole)
            .Should().BeEquivalentTo(["Manager", "Supervisor"]);
    }
}
