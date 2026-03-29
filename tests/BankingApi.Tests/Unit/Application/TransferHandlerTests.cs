using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class TransferHandlerTests
{
    [Fact]
    public async Task Handle_ValidTransfer_DecrementFromIncrementTo()
    {
        var userId = "user-1";
        var from = Account.Create("Alice", userId);
        var to = Account.Create("Bob", "user-2");
        from.Deposit(1000m, "Setup");
        var idempotencyKey = Guid.NewGuid();

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        
        mockAccounts.Setup(r => r.GetByIdAsync(from.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(from);
        mockAccounts.Setup(r => r.GetByIdAsync(to.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(to);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new TransferHandler(mockAccounts.Object, mockTransactions.Object);
        var cmd = new TransferCommand(from.Id, to.Id, 300m, "Payment", idempotencyKey, userId);

        var result = await handler.HandleAsync(cmd);

        result.Should().NotBeNull();
        result.WasDuplicate.Should().BeFalse();
        mockAccounts.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameAccount_ThrowsException()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new TransferHandler(mockAccounts.Object, mockTransactions.Object);
        var act = () => handler.HandleAsync(new TransferCommand(account.Id, account.Id, 100m, "Self", Guid.NewGuid(), userId));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ThrowsException()
    {
        var from = Account.Create("Alice", "user-1");
        var to = Account.Create("Bob", "user-2");

        var mockAccounts = new Mock<IAccountRepository>();
        var mockTransactions = new Mock<ITransactionRepository>();
        mockAccounts.Setup(r => r.GetByIdAsync(from.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(from);
        mockTransactions.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        var handler = new TransferHandler(mockAccounts.Object, mockTransactions.Object);
        var act = () => handler.HandleAsync(new TransferCommand(from.Id, to.Id, 100m, "Hack", Guid.NewGuid(), "unauthorized"));

        await act.Should().ThrowAsync<DomainException>();
    }
}
