using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class DepositHandlerTests
{
    [Fact]
    public async Task Handle_ValidDeposit_IncreasesBalance()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);
        var idempotencyKey = Guid.NewGuid();

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new DepositHandler(mockAccounts.Object, mockTransactions.Object);
        var cmd = new DepositCommand(account.Id, 1000m, "Salary", idempotencyKey, userId);

        var result = await handler.HandleAsync(cmd);

        result.Should().NotBeNull();
        result.WasDuplicate.Should().BeFalse();
        mockAccounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_ReturnsExisting()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);
        var idempotencyKey = Guid.NewGuid();
        var existingTx = Transaction.Create(account.Id, TransactionType.Deposit, 500m, "Dup");

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTx);

        var handler = new DepositHandler(mockAccounts.Object, mockTransactions.Object);
        var result = await handler.HandleAsync(new DepositCommand(account.Id, 1000m, "Salary", idempotencyKey, userId));

        result.WasDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ThrowsException()
    {
        var account = Account.Create("John", "user-1");
        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new DepositHandler(mockAccounts.Object, mockTransactions.Object);
        var act = () => handler.HandleAsync(new DepositCommand(account.Id, 100m, "Hack", Guid.NewGuid(), "unauthorized"));

        await act.Should().ThrowAsync<DomainException>();
    }
}
