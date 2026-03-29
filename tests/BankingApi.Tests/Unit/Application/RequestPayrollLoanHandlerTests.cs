using BankingApi.Application.Interfaces;
using BankingApi.Application.Loans.Commands;
using BankingApi.Domain.Enums;
using FluentAssertions;
using Moq;

namespace BankingApi.Tests.Unit.Application;

public class RequestPayrollLoanHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesLoanAndPublishesMessage()
    {
        var mockRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RequestPayrollLoanHandler(mockRepo.Object, mockPublisher.Object);
        var cmd = new RequestPayrollLoanCommand(
            ClientId: "client-1",
            Amount: 20_000m,
            Installments: 24,
            EmployerName: "TechCorp",
            MonthlySalary: 10_000m,
            EmploymentStatus: EmploymentStatus.Active,
            ExistingPayrollDeductions: 0);

        var result = await handler.Handle(cmd);

        result.Should().NotBeNull();
        result.Amount.Should().Be(20_000m);
        result.Status.Should().Be(LoanStatus.PendingApproval);
        result.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Pending);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Loan>(), It.IsAny<CancellationToken>()), Times.Once);
        mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PublishFails_SetsAiStatusToFailed()
    {
        var mockRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RequestPayrollLoanHandler(mockRepo.Object, mockPublisher.Object);
        var cmd = new RequestPayrollLoanCommand(
            ClientId: "client-1",
            Amount: 20_000m,
            Installments: 24,
            EmployerName: "TechCorp",
            MonthlySalary: 10_000m,
            EmploymentStatus: EmploymentStatus.Active,
            ExistingPayrollDeductions: 0);

        var result = await handler.Handle(cmd);

        result.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Failed);
    }
}
