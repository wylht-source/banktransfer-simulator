using BankingApi.Application.Accounts.Queries;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class GetAccountHandlerTests
{
    [Fact]
    public async Task Handle_ExistingAccount_ReturnsResult()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);

        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetAccountHandler(mockRepo.Object);
        var result = await handler.HandleAsync(new GetAccountQuery(account.Id, userId));

        result.Should().NotBeNull();
        result.OwnerName.Should().Be("John");
    }

    [Fact]
    public async Task Handle_NonExistentAccount_ThrowsException()
    {
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var handler = new GetAccountHandler(mockRepo.Object);
        var act = () => handler.HandleAsync(new GetAccountQuery(Guid.NewGuid(), "user-1"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ThrowsException()
    {
        var account = Account.Create("John", "user-1");
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetAccountHandler(mockRepo.Object);
        var act = () => handler.HandleAsync(new GetAccountQuery(account.Id, "unauthorized-user"));

        await act.Should().ThrowAsync<DomainException>();
    }
}
