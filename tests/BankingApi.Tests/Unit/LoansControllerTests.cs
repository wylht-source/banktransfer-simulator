using BankingApi.API.Controllers;
using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Loans.Queries;
using BankingApi.Application.Loans.Services;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace BankingApi.Tests.Unit;

public class LoansControllerTests
{
    // Helper: Simula o comportamento do GlobalExceptionHandlerMiddleware nos testes
    private static async Task<IActionResult> ExecuteWithExceptionHandling(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
        catch (DomainException ex) when (ex.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase))
        {
            return new ForbidResult();
        }
        catch (DomainException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

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
            new RequestLoanHandler(loanRepo, publisher, NullLogger<RequestLoanHandler>.Instance),
            new RequestPayrollLoanHandler(loanRepo, publisher, NullLogger<RequestLoanHandler>.Instance),
            new ApproveLoanHandler(loanRepo),
            new RejectLoanHandler(loanRepo),
            new CancelLoanHandler(loanRepo, NullLogger<CancelLoanHandler>.Instance),
            new GetLoanHandler(loanRepo),
            new GetMyLoansHandler(loanRepo),
            new GetPendingLoansHandler(loanRepo),
            new GetDecidedLoansHandler(loanRepo),
            new GetLoanApprovalDetailsHandler(loanRepo, new LoanProfitabilityService(), mockIdentity.Object),
            new RetryAiAnalysisHandler(loanRepo, new Mock<ILoanDocumentRepository>().Object, publisher, NullLogger<RetryAiAnalysisHandler>.Instance));

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
        mockRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);
        mockRepo.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = BuildController(mockRepo.Object, mockPublisher.Object, role: "Client");
        controller.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();

        var result = await controller.RequestLoan(new RequestLoanRequest(15_000m, 12), default);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task RequestLoan_InvalidAmount_ThrowsDomainException()
    {
        var controller = BuildController(new Mock<ILoanRepository>().Object, role: "Client");
        controller.Request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();

        var act = () => controller.RequestLoan(new RequestLoanRequest(0m, 12), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*amount*");
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
    public async Task Approve_LoanNotFound_ThrowsDomainException()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var controller = BuildController(mockRepo.Object);

        var act = () => controller.Approve(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Approve_InsufficientRole_ThrowsDomainException()
    {
        // Supervisor-level loan approved by a Manager → domain rejects with "authority"
        var loan = new PersonalLoan("client-1", 50_000m, 24); // RequiredRole = Supervisor
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var controller = BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager);

        var act = () => controller.Approve(loan.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*authority*");
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
    public async Task Reject_EmptyReason_ThrowsDomainException()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var controller = BuildController(mockRepo.Object, userId: "manager-1", role: Loan.RoleManager);

        var act = () => controller.Reject(loan.Id, new RejectLoanRequest(""), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reason*");
    }

    [Fact]
    public async Task Reject_LoanNotFound_ThrowsDomainException()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var controller = BuildController(mockRepo.Object);

        var act = () => controller.Reject(Guid.NewGuid(), new RejectLoanRequest("Too risky."), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
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
    public async Task Cancel_AlreadyApprovedLoan_ThrowsDomainException()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        loan.Approve("manager-1", Loan.RoleManager);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var controller = BuildController(mockRepo.Object, userId: "client-1", role: "Client");

        var act = () => controller.Cancel(loan.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*pending*");
    }
}
