using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Commands;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class CancelLoanHandlerTests
{
    [Fact]
    public async Task Handle_OwnerCancelsPendingLoan_Succeeds()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var handler = new CancelLoanHandler(mockRepo.Object, NullLogger<CancelLoanHandler>.Instance);
        var result = await handler.Handle(new CancelLoanCommand(loan.Id, "client-1"));

        result.Should().NotBeNull();
        result.LoanId.Should().Be(loan.Id);
        mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WrongClient_ThrowsException()
    {
        var loan = new PersonalLoan("client-1", 10_000m, 12);
        var mockRepo = new Mock<ILoanRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loan);

        var handler = new CancelLoanHandler(mockRepo.Object, NullLogger<CancelLoanHandler>.Instance);
        var act = () => handler.Handle(new CancelLoanCommand(loan.Id, "client-999"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*owner*");
    }
}
