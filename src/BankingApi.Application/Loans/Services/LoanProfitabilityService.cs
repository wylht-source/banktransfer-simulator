using BankingApi.Domain.Entities;

namespace BankingApi.Application.Loans.Services;

public record RiskParameters(decimal ProbabilityOfDefault, decimal CapitalChargeRate);

public record ProductRiskProfile(
    decimal FundingRateMonthly,
    decimal LossGivenDefault,
    decimal BaseOperationalCost,
    decimal OperationalCostRate,
    Dictionary<string, RiskParameters> RiskByRole);

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
    // ── Personal Loan parameters ─────────────────────────────────────────────
    private static readonly ProductRiskProfile PersonalProfile = new(
        FundingRateMonthly:    0.0035m,
        LossGivenDefault:      0.55m,
        BaseOperationalCost:   80m,
        OperationalCostRate:   0.007m,
        RiskByRole: new()
        {
            [Loan.RoleManager]         = new(ProbabilityOfDefault: 0.015m, CapitalChargeRate: 0.004m),
            [Loan.RoleSupervisor]      = new(ProbabilityOfDefault: 0.035m, CapitalChargeRate: 0.008m),
            [Loan.RoleCreditCommittee] = new(ProbabilityOfDefault: 0.060m, CapitalChargeRate: 0.013m),
        });

    // ── Payroll Loan parameters — lower risk profile ──────────────────────────
    private static readonly ProductRiskProfile PayrollProfile = new(
        FundingRateMonthly:    0.0035m,
        LossGivenDefault:      0.35m,
        BaseOperationalCost:   70m,
        OperationalCostRate:   0.005m,
        RiskByRole: new()
        {
            [Loan.RoleManager]         = new(ProbabilityOfDefault: 0.008m, CapitalChargeRate: 0.003m),
            [Loan.RoleSupervisor]      = new(ProbabilityOfDefault: 0.015m, CapitalChargeRate: 0.005m),
            [Loan.RoleCreditCommittee] = new(ProbabilityOfDefault: 0.025m, CapitalChargeRate: 0.008m),
        });

    public ProfitabilitySnapshot Calculate(Loan loan)
    {
        var profile = loan is PayrollLoan ? PayrollProfile : PersonalProfile;

        if (!profile.RiskByRole.TryGetValue(loan.RequiredApprovalRole, out var risk))
            throw new InvalidOperationException(
                $"No risk parameters defined for role '{loan.RequiredApprovalRole}'.");

        var totalPayable             = loan.MonthlyPayment * loan.Installments;
        var grossInterestRevenue     = totalPayable - loan.Amount;
        var estimatedFundingCost     = loan.Amount * profile.FundingRateMonthly * loan.Installments;
        var expectedCreditLoss       = loan.Amount * risk.ProbabilityOfDefault * profile.LossGivenDefault;
        var estimatedOperationalCost = profile.BaseOperationalCost + (loan.Amount * profile.OperationalCostRate);
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