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
        mockLoanRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Loan?)null);

        var handler = new RequestLoanHandler(mockLoanRepo.Object, mockPublisher.Object);
        var cmd = new RequestLoanCommand("client-1", 50_000m, 24, Guid.NewGuid());

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
        mockLoanRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Loan?)null);

        var handler = new RequestLoanHandler(mockLoanRepo.Object, mockPublisher.Object);
        var result = await handler.Handle(new RequestLoanCommand("client-1", 30_000m, 12, Guid.NewGuid()));

        result.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Failed);
    }

    [Fact]
    public async Task Handle_IdempotencyKey_ReturnsSameResultWithoutDuplicate()
    {
        var idempotencyKey = Guid.NewGuid();
        var existingLoan = new Domain.Entities.PersonalLoan(
            clientId: "client-1",
            amount: 50_000m,
            installments: 24,
            idempotencyKey: idempotencyKey);

        var mockLoanRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockLoanRepo.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLoan);

        var handler = new RequestLoanHandler(mockLoanRepo.Object, mockPublisher.Object);
        var cmd = new RequestLoanCommand("client-1", 50_000m, 24, idempotencyKey);

        var result = await handler.Handle(cmd);

        result.LoanId.Should().Be(existingLoan.Id);
        result.Amount.Should().Be(50_000m);
        result.Status.Should().Be(existingLoan.Status);
        mockLoanRepo.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Loan>(), It.IsAny<CancellationToken>()), Times.Never);
        mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
