using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class PayrollLoanTests
{
    [Fact]
    public void Create_ValidParams_CreatesSuccessfully()
    {
        var loan = new PayrollLoan(
            "client-1", 30_000m, 24, "TechCorp", 10_000m,
            EmploymentStatus.Active, 0);

        loan.ClientId.Should().Be("client-1");
        loan.Amount.Should().Be(30_000m);
        loan.EmployerName.Should().Be("TechCorp");
    }

    [Fact]
    public void Create_InvalidSalary_ThrowsException()
    {
        var act = () => new PayrollLoan(
            "client-1", 30_000m, 24, "", 0, EmploymentStatus.Active, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PayrollMarginExceeded_ThrowsException()
    {
        var act = () => new PayrollLoan(
            "client-1", 50_000m, 24, "TechCorp", 5_000m,
            EmploymentStatus.Active, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_InactiveEmployment_ThrowsException()
    {
        var act = () => new PayrollLoan(
            "client-1", 10_000m, 24, "TechCorp", 8_000m,
            EmploymentStatus.Inactive, 0);

        act.Should().Throw<DomainException>().WithMessage("*active employment*");
    }

    [Fact]
    public void Create_ValidParams_CalculatesPayrollMarginCorrectly()
    {
        // 35% of 10,000 salary = 3,500 margin limit
        var loan = new PayrollLoan(
            "client-1", 10_000m, 24, "TechCorp", 10_000m,
            EmploymentStatus.Active, 0);

        loan.PayrollMarginLimit.Should().Be(3_500m);
        loan.AvailablePayrollMargin.Should().Be(3_500m);
        loan.RemainingPayrollMargin.Should().Be(3_500m - loan.MonthlyPayment);
    }

    [Fact]
    public void Create_WithExistingDeductions_ReducesAvailableMargin()
    {
        // Salary 10,000 → margin 3,500. Existing deductions 1,000 → available 2,500
        var loan = new PayrollLoan(
            "client-1", 5_000m, 12, "TechCorp", 10_000m,
            EmploymentStatus.Active, existingPayrollDeductions: 1_000m);

        loan.AvailablePayrollMargin.Should().Be(2_500m);
    }
}
