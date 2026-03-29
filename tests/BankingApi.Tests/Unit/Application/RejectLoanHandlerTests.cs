using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Commands;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class RejectLoanHandlerTests
{
    [Fact]
    public async Task Handle_ExistingLoan_RejectsSuccessfully()
    {
        var loan = new PersonalLoan("client-1", 15_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var handler = new RejectLoanHandler(mockRepo.Object);
        var result = await handler.Handle(new RejectLoanCommand(loan.Id, "manager-1", Loan.RoleManager, "Insufficient credit history."));

        result.Should().NotBeNull();
        result.RejectionReason.Should().Be("Insufficient credit history.");
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentLoan_ThrowsException()
    {
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Loan?)null);

        var handler = new RejectLoanHandler(mockRepo.Object);
        var act = () => handler.Handle(new RejectLoanCommand(Guid.NewGuid(), "manager-1", Loan.RoleManager, "Too risky."));

        await act.Should().ThrowAsync<DomainException>();
    }
}
