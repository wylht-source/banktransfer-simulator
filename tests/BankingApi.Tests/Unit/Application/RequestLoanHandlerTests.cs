using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Interfaces;
using BankingApi.Domain.Enums;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class RequestLoanHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesLoanAndPublishesMessage()
    {
        var mockLoanRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RequestLoanHandler(mockLoanRepo.Object, mockPublisher.Object);
        var cmd = new RequestLoanCommand("client-1", 50_000m, 24);

        var result = await handler.Handle(cmd);

        result.Should().NotBeNull();
        result.Amount.Should().Be(50_000m);
        result.Status.Should().Be(LoanStatus.PendingApproval);
        mockLoanRepo.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Loan>(), It.IsAny<CancellationToken>()), Times.Once);
        mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PublishFails_SetStatusFailed()
    {
        var mockLoanRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RequestLoanHandler(mockLoanRepo.Object, mockPublisher.Object);
        var result = await handler.Handle(new RequestLoanCommand("client-1", 30_000m, 12));

        result.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Failed);
    }
}
