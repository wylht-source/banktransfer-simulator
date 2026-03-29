using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class CreateAccountHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesAndReturnsResult()
    {
        var mockRepo = new Mock<IAccountRepository>();
        var handler = new CreateAccountHandler(mockRepo.Object);

        var cmd = new CreateAccountCommand("John Doe", "user-123");
        var result = await handler.HandleAsync(cmd);

        result.Should().NotBeNull();
        result.OwnerName.Should().Be("John Doe");
        result.Id.Should().NotBeEmpty();
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
