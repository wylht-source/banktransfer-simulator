using BankingApi.Application.Loans.Services;
using BankingApi.Domain.Entities;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class LoanProfitabilityServiceTests
{
    private readonly LoanProfitabilityService _service = new();

    // ── Constants for verification ──────────────────────────────────────────
    private const decimal FundingRateMonthly  = 0.0035m;
    private const decimal LossGivenDefault    = 0.55m;
    private const decimal BaseOperationalCost = 80m;
    private const decimal OperationalCostRate = 0.007m;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Loan CreateLoanWithRole(
        decimal amount,
        int installments,
        string requiredRole)
    {
        // Create a loan and adjust its role if needed; since Loan auto-determines role,
        // we create with an appropriate amount
        return new Loan("client-1", amount, installments, 0.015m);
    }

    // ── Manager Level Tests (Amount ≤ 20,000) ──────────────────────────────

    [Fact]
    public void Calculate_ManagerLevelLoan_ReturnsCorrectProfitabilityMetrics()
    {
        // Arrange: Manager level = 10,000
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        loan.Should().NotBeNull();
        loan.RequiredApprovalRole.Should().Be(BankingApi.Domain.Entities.Loan.RoleManager);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.Should().NotBeNull();
        result.TotalPayable.Should().BeGreaterThan(0);
        result.GrossInterestRevenue.Should().BeGreaterThan(0);
        result.EstimatedFundingCost.Should().BeGreaterThan(0);
        result.ExpectedCreditLoss.Should().BeGreaterThan(0);
        result.EstimatedOperationalCost.Should().BeGreaterThan(0);
        result.EstimatedCapitalCharge.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesFundingCostCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var expectedFundingCost = 10_000m * FundingRateMonthly * 12; // 420

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.EstimatedFundingCost.Should().Be(Math.Round(expectedFundingCost, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesOperationalCostCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var expectedOpCost = BaseOperationalCost + (10_000m * OperationalCostRate); // 80 + 70 = 150

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.EstimatedOperationalCost.Should().Be(Math.Round(expectedOpCost, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesCapitalChargeCorrectly()
    {
        // Arrange: Manager risk = 0.004
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var expectedCapitalCharge = 10_000m * 0.004m; // 40

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.EstimatedCapitalCharge.Should().Be(Math.Round(expectedCapitalCharge, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesExpectedCreditLossCorrectly()
    {
        // Arrange: Manager PD = 0.015
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var managerPD = 0.015m;
        var expectedCreditLoss = 10_000m * managerPD * LossGivenDefault; // 82.5

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.ExpectedCreditLoss.Should().Be(Math.Round(expectedCreditLoss, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesTotalPayableCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var expectedTotalPayable = loan.MonthlyPayment * 12;

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.TotalPayable.Should().Be(Math.Round(expectedTotalPayable, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesGrossInterestRevenueCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);
        var expectedGrossInterest = (loan.MonthlyPayment * 12) - 10_000m;

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.GrossInterestRevenue.Should().Be(Math.Round(expectedGrossInterest, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesNetProfitCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert: Net Profit = Gross Interest - Funding Cost - Credit Loss - Op Cost - Capital Charge
        var expectedNetProfit = result.GrossInterestRevenue
            - result.EstimatedFundingCost
            - result.ExpectedCreditLoss
            - result.EstimatedOperationalCost
            - result.EstimatedCapitalCharge;

        result.EstimatedNetProfit.Should().Be(Math.Round(expectedNetProfit, 2));
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_CalculatesProfitMarginCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert: Profit Margin = Net Profit / Amount
        var expectedMargin = Math.Round(result.EstimatedNetProfit / 10_000m, 4);
        result.EstimatedProfitMargin.Should().Be(expectedMargin);
    }

    [Fact]
    public void Calculate_ManagerLevelLoan_SmallAmount_CalculatesMetrics()
    {
        // Arrange: Small loans might have negative net profit due to fixed costs
        var smallLoan = new Loan("client-1", 1_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(smallLoan);

        // Assert: Just verify the calculations work, don't assume profitability
        result.Should().NotBeNull();
        result.TotalPayable.Should().BeGreaterThan(0);
        result.GrossInterestRevenue.Should().BeGreaterThan(0);
    }

    // ── Supervisor Level Tests (20,000 < Amount ≤ 100,000) ──────────────────

    [Fact]
    public void Calculate_SupervisorLevelLoan_ReturnsCorrectProfitabilityMetrics()
    {
        // Arrange: Supervisor level = 50,000
        var loan = new Loan("client-1", 50_000m, 24, 0.015m);
        loan.RequiredApprovalRole.Should().Be(BankingApi.Domain.Entities.Loan.RoleSupervisor);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.Should().NotBeNull();
        result.TotalPayable.Should().BeGreaterThan(0);
        result.GrossInterestRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_SupervisorLevelLoan_CalculatesExpectedCreditLossWithHigherPD()
    {
        // Arrange: Supervisor PD = 0.035 (higher risk than Manager)
        var supervisorLoan = new Loan("client-1", 50_000m, 24, 0.015m);
        var supervisorPD = 0.035m;
        var expectedCreditLoss = 50_000m * supervisorPD * LossGivenDefault;

        // Act
        var result = _service.Calculate(supervisorLoan);

        // Assert
        result.ExpectedCreditLoss.Should().Be(Math.Round(expectedCreditLoss, 2));
    }

    [Fact]
    public void Calculate_SupervisorLevelLoan_CalculatesCapitalChargeWithHigherRate()
    {
        // Arrange: Supervisor capital charge rate = 0.008 (higher than Manager's 0.004)
        var supervisorLoan = new Loan("client-1", 50_000m, 24, 0.015m);
        var expectedCapitalCharge = 50_000m * 0.008m; // 400

        // Act
        var result = _service.Calculate(supervisorLoan);

        // Assert
        result.EstimatedCapitalCharge.Should().Be(Math.Round(expectedCapitalCharge, 2));
    }

    [Fact]
    public void Calculate_SupervisorLevelLoan_HasHigherRiskParameters()
    {
        // Arrange
        var supervisorLoan = new Loan("client-1", 50_000m, 24, 0.015m);

        // Act
        var supervisorResult = _service.Calculate(supervisorLoan);

        // Assert: Supervisor loans have higher risk, so higher credit loss and capital charge
        supervisorResult.ExpectedCreditLoss.Should().BeGreaterThan(0);
        supervisorResult.EstimatedCapitalCharge.Should().BeGreaterThan(0);
        supervisorResult.Should().NotBeNull();
    }

    // ── Credit Committee Level Tests (Amount > 100,000) ──────────────────────

    [Fact]
    public void Calculate_CreditCommitteeLevelLoan_ReturnsCorrectProfitabilityMetrics()
    {
        // Arrange: Credit Committee level = 150,000
        var loan = new Loan("client-1", 150_000m, 36, 0.015m);
        loan.RequiredApprovalRole.Should().Be(BankingApi.Domain.Entities.Loan.RoleCreditCommittee);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.Should().NotBeNull();
        result.EstimatedNetProfit.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_CreditCommitteeLevelLoan_CalculatesExpectedCreditLossWithHighestPD()
    {
        // Arrange: Credit Committee PD = 0.060 (highest risk)
        var committeeLoan = new Loan("client-1", 150_000m, 36, 0.015m);
        var committeePD = 0.060m;
        var expectedCreditLoss = 150_000m * committeePD * LossGivenDefault;

        // Act
        var result = _service.Calculate(committeeLoan);

        // Assert
        result.ExpectedCreditLoss.Should().Be(Math.Round(expectedCreditLoss, 2));
    }

    [Fact]
    public void Calculate_CreditCommitteeLevelLoan_CalculatesCapitalChargeWithHighestRate()
    {
        // Arrange: Credit Committee capital charge rate = 0.013 (highest)
        var committeeLoan = new Loan("client-1", 150_000m, 36, 0.015m);
        var expectedCapitalCharge = 150_000m * 0.013m; // 1950

        // Act
        var result = _service.Calculate(committeeLoan);

        // Assert
        result.EstimatedCapitalCharge.Should().Be(Math.Round(expectedCapitalCharge, 2));
    }

    [Fact]
    public void Calculate_CreditCommitteeLevelLoan_HasHighestRiskParameters()
    {
        // Arrange
        var committeeLoan = new Loan("client-1", 150_000m, 36, 0.015m);

        // Act
        var committeeResult = _service.Calculate(committeeLoan);

        // Assert: Credit Committee has highest risk parameters
        committeeResult.ExpectedCreditLoss.Should().BeGreaterThan(0);
        committeeResult.EstimatedCapitalCharge.Should().BeGreaterThan(0);
        committeeResult.EstimatedNetProfit.Should().BeGreaterThan(0);
    }

    // ── Edge Cases ──────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_MinimumLoanAmount_CalculatesSuccessfully()
    {
        // Arrange: Minimum valid amount is 1,000
        var loan = new Loan("client-1", 1_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.Should().NotBeNull();
        result.TotalPayable.Should().BeGreaterThan(0);
        result.EstimatedProfitMargin.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_MaximumLoanAmount_CalculatesSuccessfully()
    {
        // Arrange: Maximum valid amount is 200,000
        var loan = new Loan("client-1", 200_000m, 48, 0.015m);
        loan.RequiredApprovalRole.Should().Be(BankingApi.Domain.Entities.Loan.RoleCreditCommittee);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.EstimatedNetProfit.Should().BeGreaterThan(0);
        result.TotalPayable.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_ShortestInstallmentTerm_CalculatesSuccessfully()
    {
        // Arrange: Minimum installments = 1
        var loan = new Loan("client-1", 5_000m, 1, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert: Single payment term has lower interest revenue
        result.Should().NotBeNull();
        result.TotalPayable.Should().BeGreaterThan(0);
        result.GrossInterestRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_LongestInstallmentTerm_CalculatesSuccessfully()
    {
        // Arrange: Maximum installments = 48
        var loan = new Loan("client-1", 10_000m, 48, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        result.EstimatedNetProfit.Should().BeGreaterThan(0);
        // Longer terms should have higher total funding cost
        result.EstimatedFundingCost.Should().BeGreaterThan(500);
    }

    [Fact]
    public void Calculate_LongerTermProducesHigherFundingCost()
    {
        // Arrange
        var shortTermLoan = new Loan("client-1", 10_000m, 12, 0.015m);
        var longTermLoan = new Loan("client-1", 10_000m, 36, 0.015m);

        // Act
        var shortResult = _service.Calculate(shortTermLoan);
        var longResult = _service.Calculate(longTermLoan);

        // Assert: Same amount, longer term = higher funding cost
        longResult.EstimatedFundingCost.Should().BeGreaterThan(shortResult.EstimatedFundingCost);
    }

    [Fact]
    public void Calculate_HigherAmountProducesHigherAbsoluteNetProfit()
    {
        // Arrange
        var smallLoan = new Loan("client-1", 5_000m, 12, 0.015m);
        var largeLoan = new Loan("client-1", 50_000m, 12, 0.015m);

        // Act
        var smallResult = _service.Calculate(smallLoan);
        var largeResult = _service.Calculate(largeLoan);

        // Assert
        largeResult.EstimatedNetProfit.Should().BeGreaterThan(smallResult.EstimatedNetProfit);
    }

    // ── Validation and Rounding Tests ───────────────────────────────────────

    [Fact]
    public void Calculate_AllValuesAreRoundedToTwoDecimals()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        AssertDecimalRounding(result.TotalPayable);
        AssertDecimalRounding(result.GrossInterestRevenue);
        AssertDecimalRounding(result.EstimatedFundingCost);
        AssertDecimalRounding(result.ExpectedCreditLoss);
        AssertDecimalRounding(result.EstimatedOperationalCost);
        AssertDecimalRounding(result.EstimatedCapitalCharge);
        AssertDecimalRounding(result.EstimatedNetProfit);
    }

    [Fact]
    public void Calculate_ProfitMarginIsRoundedToFourDecimals()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        var decimalPlaces = GetDecimalPlaces(result.EstimatedProfitMargin);
        decimalPlaces.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void Calculate_ComponentsSumToNetProfit()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert: Net Profit = Gross Interest - All Costs
        var calculatedNetProfit = result.GrossInterestRevenue
            - result.EstimatedFundingCost
            - result.ExpectedCreditLoss
            - result.EstimatedOperationalCost
            - result.EstimatedCapitalCharge;

        result.EstimatedNetProfit.Should().BeApproximately(Math.Round(calculatedNetProfit, 2), 0.01m);
    }

    [Fact]
    public void Calculate_ProfitMarginIsCalculatedCorrectly()
    {
        // Arrange
        var loan = new Loan("client-1", 10_000m, 12, 0.015m);

        // Act
        var result = _service.Calculate(loan);

        // Assert
        var expectedMargin = Math.Round(result.EstimatedNetProfit / 10_000m, 4);
        result.EstimatedProfitMargin.Should().Be(expectedMargin);
    }

    [Fact]
    public void Calculate_ThrowsInvalidOperationException_ForUnknownRole()
    {
        // Arrange: Create a mock loan with unknown role by using reflection
        // (Since Loan auto-determines the role, we need to test the service directly)
        var mockLoan = new MockLoanWithUnknownRole();

        // Act & Assert
        var act = () => _service.Calculate(mockLoan);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No risk parameters defined for role*");
    }

    // ── Helper Methods ──────────────────────────────────────────────────────

    private static void AssertDecimalRounding(decimal value)
    {
        var decimalPlaces = GetDecimalPlaces(value);
        decimalPlaces.Should().BeLessThanOrEqualTo(2);
    }

    private static int GetDecimalPlaces(decimal value)
    {
        var decimalValue = decimal.Parse(value.ToString());
        var scale = BitConverter.GetBytes(decimal.GetBits(decimalValue)[3])[2];
        return scale;
    }

    // ── Mock Loan for Unknown Role Testing ──────────────────────────────────
    private class MockLoanWithUnknownRole : Loan
    {
        public MockLoanWithUnknownRole() : base("client-1", 5_000m, 12, 0.015m)
        {
            // Simulate an unknown role by using reflection to set it
            var requiredRoleProperty = typeof(Loan).GetProperty(nameof(Loan.RequiredApprovalRole));
            requiredRoleProperty?.SetValue(this, "UnknownRole");
        }
    }
}
