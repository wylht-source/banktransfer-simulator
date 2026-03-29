using BankingApi.Application.Accounts.Queries;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class GetAccountByOwnerHandlerTests
{
    [Fact]
    public async Task Handle_ValidOwner_ReturnsAccount()
    {
        var userId = "user-1";
        var account = Account.Create("John", userId);
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByOwnerIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var handler = new GetAccountByOwnerHandler(mockRepo.Object);
        var result = await handler.HandleAsync(new GetAccountByOwnerQuery(userId));

        result.Should().NotBeNull();
        result.OwnerName.Should().Be("John");
    }

    [Fact]
    public async Task Handle_NonExistentOwner_ThrowsException()
    {
        var mockRepo = new Mock<IAccountRepository>();
        mockRepo.Setup(r => r.GetByOwnerIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        var handler = new GetAccountByOwnerHandler(mockRepo.Object);
        var act = () => handler.HandleAsync(new GetAccountByOwnerQuery("nonexistent"));

        await act.Should().ThrowAsync<DomainException>();
    }
}
