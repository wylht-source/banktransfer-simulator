using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class WithdrawHandlerTests
{
    [Fact]
    public async Task Handle_ValidWithdraw_DecreasesBalance()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);
        account.Deposit(1000m, "Setup");
        var idempotencyKey = Guid.NewGuid();

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new WithdrawHandler(mockAccounts.Object, mockTransactions.Object);
        var cmd = new WithdrawCommand(account.Id, 300m, "Rent", idempotencyKey, userId);

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
        account.Deposit(1000m, "Setup");
        var idempotencyKey = Guid.NewGuid();
        var existingTx = Transaction.Create(account.Id, Domain.Enums.TransactionType.Withdrawal, 300m, "Rent");

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTx);

        var handler = new WithdrawHandler(mockAccounts.Object, mockTransactions.Object);
        var result = await handler.HandleAsync(new WithdrawCommand(account.Id, 300m, "Rent", idempotencyKey, userId));

        result.WasDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InsufficientFunds_ThrowsException()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);
        account.Deposit(50m, "Setup");

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new WithdrawHandler(mockAccounts.Object, mockTransactions.Object);
        var act = () => handler.HandleAsync(new WithdrawCommand(account.Id, 1000m, "Too much", Guid.NewGuid(), userId));

        await act.Should().ThrowAsync<DomainException>();
    }
}
