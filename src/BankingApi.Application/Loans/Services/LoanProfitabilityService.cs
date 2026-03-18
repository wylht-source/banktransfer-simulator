using BankingApi.Domain.Entities;

namespace BankingApi.Application.Loans.Services;

public record RiskParameters(decimal ProbabilityOfDefault, decimal CapitalChargeRate);

public record ProfitabilitySnapshot(
    decimal TotalPayable,
    decimal GrossInterestRevenue,
    decimal EstimatedFundingCost,
    decimal ExpectedCreditLoss,
    decimal EstimatedOperationalCost,
    decimal EstimatedCapitalCharge,
    decimal EstimatedNetProfit,
    decimal EstimatedProfitMargin);

public class LoanProfitabilityService
{
    // ── Shared parameters ────────────────────────────────────────────────────
    private const decimal FundingRateMonthly    = 0.0035m;  // 0.35%
    private const decimal LossGivenDefault      = 0.55m;    // 55%
    private const decimal BaseOperationalCost   = 80m;
    private const decimal OperationalCostRate   = 0.007m;   // 0.7%

    // ── Risk parameters per approval role ────────────────────────────────────
    private static readonly Dictionary<string, RiskParameters> RiskByRole = new()
    {
        [Loan.RoleManager]         = new(ProbabilityOfDefault: 0.015m, CapitalChargeRate: 0.004m),
        [Loan.RoleSupervisor]      = new(ProbabilityOfDefault: 0.035m, CapitalChargeRate: 0.008m),
        [Loan.RoleCreditCommittee] = new(ProbabilityOfDefault: 0.060m, CapitalChargeRate: 0.013m),
    };

    public ProfitabilitySnapshot Calculate(Loan loan)
    {
        if (!RiskByRole.TryGetValue(loan.RequiredApprovalRole, out var risk))
            throw new InvalidOperationException($"No risk parameters defined for role '{loan.RequiredApprovalRole}'.");

        var totalPayable             = loan.MonthlyPayment * loan.Installments;
        var grossInterestRevenue     = totalPayable - loan.Amount;
        var estimatedFundingCost     = loan.Amount * FundingRateMonthly * loan.Installments;
        var expectedCreditLoss       = loan.Amount * risk.ProbabilityOfDefault * LossGivenDefault;
        var estimatedOperationalCost = BaseOperationalCost + (loan.Amount * OperationalCostRate);
        var estimatedCapitalCharge   = loan.Amount * risk.CapitalChargeRate;

        var estimatedNetProfit = grossInterestRevenue
            - estimatedFundingCost
            - expectedCreditLoss
            - estimatedOperationalCost
            - estimatedCapitalCharge;

        var estimatedProfitMargin = loan.Amount == 0 ? 0m
            : Math.Round(estimatedNetProfit / loan.Amount, 4);

        return new ProfitabilitySnapshot(
            TotalPayable:             Math.Round(totalPayable, 2),
            GrossInterestRevenue:     Math.Round(grossInterestRevenue, 2),
            EstimatedFundingCost:     Math.Round(estimatedFundingCost, 2),
            ExpectedCreditLoss:       Math.Round(expectedCreditLoss, 2),
            EstimatedOperationalCost: Math.Round(estimatedOperationalCost, 2),
            EstimatedCapitalCharge:   Math.Round(estimatedCapitalCharge, 2),
            EstimatedNetProfit:       Math.Round(estimatedNetProfit, 2),
            EstimatedProfitMargin:    estimatedProfitMargin);
    }
}
