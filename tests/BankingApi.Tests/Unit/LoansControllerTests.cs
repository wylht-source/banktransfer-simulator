using BankingApi.API.Controllers;
using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Loans.Queries;
using BankingApi.Application.Loans.Services;
using BankingApi.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BankingApi.Tests.Unit;

public class LoansControllerTests
{
    private static LoansController BuildController(
        ILoanRepository loanRepo,
        IMessagePublisher? publisher = null,
        string userId = "manager-1",
        string role = Loan.RoleManager)
    {
        publisher ??= new Mock<IMessagePublisher>().Object;

        var mockIdentity = new Mock<IIdentityService>();
        mockIdentity.Setup(s => s.GetDisplayNameAsync(It.IsAny<string>()))
            .ReturnsAsync("Test User");

        var controller = new LoansController(
            new RequestLoanHandler(loanRepo, publisher),
            new RequestPayrollLoanHandler(loanRepo, publisher),
            new ApproveLoanHandler(loanRepo),
            new RejectLoanHandler(loanRepo),
            new CancelLoanHandler(loanRepo),
            new GetLoanHandler(loanRepo),
            new GetMyLoansHandler(loanRepo),
            new GetPendingLoansHandler(loanRepo),
            new GetDecidedLoansHandler(loanRepo),
            new GetLoanApprovalDetailsHandler(loanRepo, new LoanProfitabilityService(), mockIdentity.Object),
            new RetryAiAnalysisHandler(loanRepo, new Mock<ILoanDocumentRepository>().Object, publisher));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, role)
                ], "test"))
            }
        };

        return controller;
    }

    // ── RequestLoan ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestLoan_ValidRequest_Returns201Created()
    {
        var mockRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await BuildController(mockRepo.Object, mockPublisher.Object, role: "Client")
            .RequestLoan(new RequestLoanRequest(15_000m, 12), default);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task RequestLoan_InvalidAmount_Returns400BadRequest()
    {
        var result = await BuildController(new Mock<ILoanRepository>().Object, role: "Client")
            .RequestLoan(new RequestLoanRequest(0m, 12), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_ExistingLoan_Returns200Ok()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager)
            .Approve(loan.Id, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Approve_LoanNotFound_Returns404NotFound()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var result = await BuildController(mockRepo.Object)
            .Approve(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Approve_InsufficientRole_Returns400BadRequest()
    {
        // Supervisor-level loan approved by a Manager → domain rejects with "authority"
        var loan = new PersonalLoan("client-1", 50_000m, 24); // RequiredRole = Supervisor
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager)
            .Approve(loan.Id, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WithValidReason_Returns200Ok()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager)
            .Reject(loan.Id, new RejectLoanRequest("Insufficient credit history."), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Reject_EmptyReason_Returns400BadRequest()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager)
            .Reject(loan.Id, new RejectLoanRequest(""), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Reject_LoanNotFound_Returns404NotFound()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var result = await BuildController(mockRepo.Object)
            .Reject(Guid.NewGuid(), new RejectLoanRequest("Too risky."), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_OwnPendingLoan_Returns200Ok()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "client-1", role: "Client")
            .Cancel(loan.Id, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cancel_AlreadyApprovedLoan_Returns400BadRequest()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        loan.Approve("manager-1", Loan.RoleManager);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var result = await BuildController(mockRepo.Object, userId: "client-1", role: "Client")
            .Cancel(loan.Id, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
