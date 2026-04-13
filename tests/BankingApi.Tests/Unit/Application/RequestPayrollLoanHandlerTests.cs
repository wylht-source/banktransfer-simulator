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
        mockRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Loan?)null);

        var handler = new RequestPayrollLoanHandler(mockRepo.Object, mockPublisher.Object);
        var cmd = new RequestPayrollLoanCommand(
            ClientId: "client-1",
            Amount: 20_000m,
            Installments: 24,
            EmployerName: "TechCorp",
            MonthlySalary: 10_000m,
            EmploymentStatus: EmploymentStatus.Active,
            ExistingPayrollDeductions: 0,
            IdempotencyKey: Guid.NewGuid());

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
        mockRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Loan?)null);

        var handler = new RequestPayrollLoanHandler(mockRepo.Object, mockPublisher.Object);
        var cmd = new RequestPayrollLoanCommand(
            ClientId: "client-1",
            Amount: 20_000m,
            Installments: 24,
            EmployerName: "TechCorp",
            MonthlySalary: 10_000m,
            EmploymentStatus: EmploymentStatus.Active,
            ExistingPayrollDeductions: 0,
            IdempotencyKey: Guid.NewGuid());

        var result = await handler.Handle(cmd);

        result.AiAnalysisStatus.Should().Be(AiAnalysisStatus.Failed);
    }

    [Fact]
    public async Task Handle_IdempotencyKey_ReturnsSameResultWithoutDuplicate()
    {
        var idempotencyKey = Guid.NewGuid();
        var existingLoan = new Domain.Entities.PayrollLoan(
            clientId: "client-1",
            amount: 20_000m,
            installments: 24,
            employerName: "TechCorp",
            monthlySalary: 10_000m,
            employmentStatus: EmploymentStatus.Active,
            existingPayrollDeductions: 0,
            idempotencyKey: idempotencyKey);

        var mockRepo = new Mock<ILoanRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        mockRepo.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLoan);

        var handler = new RequestPayrollLoanHandler(mockRepo.Object, mockPublisher.Object);
        var cmd = new RequestPayrollLoanCommand(
            ClientId: "client-1",
            Amount: 20_000m,
            Installments: 24,
            EmployerName: "TechCorp",
            MonthlySalary: 10_000m,
            EmploymentStatus: EmploymentStatus.Active,
            ExistingPayrollDeductions: 0,
            IdempotencyKey: idempotencyKey);

        var result = await handler.Handle(cmd);

        result.LoanId.Should().Be(existingLoan.Id);
        result.Amount.Should().Be(20_000m);
        result.Status.Should().Be(existingLoan.Status);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Loan>(), It.IsAny<CancellationToken>()), Times.Never);
        mockPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
