using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class ApproveLoanHandlerTests
{
    [Fact]
    public async Task Handle_ExistingLoan_ApprovesSuccessfully()
    {
        var loan = new PersonalLoan("client-1", 15_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var handler = new ApproveLoanHandler(mockRepo.Object);
        var cmd = new ApproveLoanCommand(loan.Id, "manager-1", Loan.RoleManager);

        var result = await handler.Handle(cmd);

        result.Should().NotBeNull();
        result.ApprovedBy.Should().Be("manager-1");
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentLoan_ThrowsException()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var handler = new ApproveLoanHandler(mockRepo.Object);
        var act = () => handler.Handle(new ApproveLoanCommand(Guid.NewGuid(), "mgr", "Manager"));

        await act.Should().ThrowAsync<DomainException>();
    }
}
