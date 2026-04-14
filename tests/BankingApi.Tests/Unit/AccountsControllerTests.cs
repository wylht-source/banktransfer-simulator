using BankingApi.API.Controllers;
using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Accounts.Queries;
using BankingApi.Application.Interfaces;
using BankingApi.Application.Transactions.Queries;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BankingApi.Tests.Unit;

public class AccountsControllerTests
{
    private static AccountsController BuildController(
        IAccountRepository accountRepo,
        ITransactionRepository? txRepo = null,
        string userId = "user-1")
    {
        txRepo ??= new Mock<ITransactionRepository>().Object;

        var controller = new AccountsController(
            new CreateAccountHandler(accountRepo),
            new GetAccountHandler(accountRepo),
            new GetStatementHandler(accountRepo, txRepo),
            new GetAccountByOwnerHandler(accountRepo));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
            }
        };

        return controller;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201Created()
    {
        var controller = BuildController(new Mock<IAccountRepository>().Object);

        var result = await controller.Create(new CreateAccountRequest("John Doe"), default);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Create_EmptyOwnerName_ThrowsDomainException()
    {
        var controller = BuildController(new Mock<IAccountRepository>().Object);

        var act = () => controller.Create(new CreateAccountRequest(""), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*required*");
    }

    // ── GetMe ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_AccountExists_Returns200Ok()
    {
        var account = Account.Create("John", "user-1");
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByOwnerIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await BuildController(mockRepo.Object).GetMe(default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<GetAccountResult>();
    }

    [Fact]
    public async Task GetMe_AccountNotFound_ThrowsDomainException()
    {
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var controller = BuildController(mockRepo.Object);

        var act = () => controller.GetMe(default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_OwnAccount_Returns200Ok()
    {
        var account = Account.Create("John", "user-1");
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await BuildController(mockRepo.Object).GetById(account.Id, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_AccountNotFound_ThrowsDomainException()
    {
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var controller = BuildController(mockRepo.Object);

        var act = () => controller.GetById(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task GetById_OtherUsersAccount_ThrowsDomainException()
    {
        var account = Account.Create("Alice", "user-2");
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // user-1 requests an account owned by user-2
        var controller = BuildController(mockRepo.Object, userId: "user-1");

        var act = () => controller.GetById(account.Id, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Access denied*");
    }

    // ── GetStatement ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatement_OwnAccount_Returns200Ok()
    {
        var account = Account.Create("John", "user-1");
        var mockRepo = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTx.Setup(r => r.GetPagedByAccountIdAsync(account.Id, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Transaction>(), 0));

        var result = await BuildController(mockRepo.Object, mockTx.Object)
            .GetStatement(account.Id, ct: default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStatement_OtherUsersAccount_ThrowsDomainException()
    {
        var account = Account.Create("Alice", "user-2");
        var mockRepo = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var controller = BuildController(mockRepo.Object, mockTx.Object, userId: "user-1");

        var act = () => controller.GetStatement(account.Id, ct: default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Access denied*");
    }
}
