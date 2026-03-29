using BankingApi.API.Controllers;
using BankingApi.Application.Interfaces;
using BankingApi.Application.Transactions.Commands;
using BankingApi.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace BankingApi.Tests.Unit;

public class TransactionsControllerTests
{
    private static TransactionsController BuildController(
        IAccountRepository accountRepo,
        ITransactionRepository txRepo,
        string userId = "user-1")
    {
        var controller = new TransactionsController(
            new DepositHandler(accountRepo, txRepo),
            new WithdrawHandler(accountRepo, txRepo),
            new TransferHandler(accountRepo, txRepo),
            accountRepo);

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

    // ── Deposit ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_ValidRequest_Returns200Ok()
    {
        var account = Account.Create("John", "user-1");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTx.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object)
            .Deposit(Guid.NewGuid(), new DepositRequest(account.Id, 500m, "Salary"), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Deposit_UnauthorizedUser_Returns400BadRequest()
    {
        var account = Account.Create("John", "user-1");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTx.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object, userId: "attacker")
            .Deposit(Guid.NewGuid(), new DepositRequest(account.Id, 500m, "Hack"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Withdraw ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_ValidRequest_Returns200Ok()
    {
        var account = Account.Create("John", "user-1");
        account.Deposit(1000m, "Setup");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTx.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object)
            .Withdraw(Guid.NewGuid(), new WithdrawRequest(account.Id, 300m, "Rent"), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Withdraw_InsufficientFunds_Returns400BadRequest()
    {
        var account = Account.Create("John", "user-1");
        account.Deposit(100m, "Setup");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTx.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object)
            .Withdraw(Guid.NewGuid(), new WithdrawRequest(account.Id, 9999m, "Too much"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Transfer ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_DestinationNotFound_Returns404NotFound()
    {
        var fromAccount = Account.Create("John", "user-1");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object)
            .Transfer(Guid.NewGuid(), new TransferRequest(fromAccount.Id, "ACC-UNKNOWN", 100m, "Payment"), default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Transfer_ValidRequest_Returns200Ok()
    {
        var from = Account.Create("Alice", "user-1");
        var to = Account.Create("Bob", "user-2");
        from.Deposit(1000m, "Setup");
        var idempotencyKey = Guid.NewGuid();

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTx = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(from.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(from);
        mockAccounts.Setup(r => r.GetByIdAsync(to.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(to);
        mockAccounts.Setup(r => r.GetByAccountNumberAsync(to.AccountNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(to);
        mockTx.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var result = await BuildController(mockAccounts.Object, mockTx.Object)
            .Transfer(idempotencyKey, new TransferRequest(from.Id, to.AccountNumber, 300m, "Payment"), default);

        result.Should().BeOfType<OkObjectResult>();
    }
}
