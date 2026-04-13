using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Domain.Entities;

public class PayrollLoan : Loan
{
    private const decimal MonthlyInterestRate      = 0.009m;   // 0.9%
    private const decimal PayrollSupervisorLimit   = 60_000m;
    private const decimal PayrollMarginPercentage  = 0.35m;    // 35% consignável

    // ── Payroll-specific properties ──────────────────────────────────────────
    public string           EmployerName              { get; private set; } = null!;
    public decimal          MonthlySalary             { get; private set; }
    public EmploymentStatus EmploymentStatus          { get; private set; }
    public decimal          ExistingPayrollDeductions { get; private set; }

    // ── EF Core constructor ──────────────────────────────────────────────────
    private PayrollLoan() { }

    // ── Factory constructor ──────────────────────────────────────────────────
    public PayrollLoan(
        string clientId,
        decimal amount,
        int installments,
        string employerName,
        decimal monthlySalary,
        EmploymentStatus employmentStatus,
        decimal existingPayrollDeductions,
        Guid? idempotencyKey = null)
    {
        Validate(amount, installments, employerName, monthlySalary, employmentStatus);

        // Calculate provisional MonthlyPayment to validate margin
        var provisionalPmt = CalculatePmt(amount, MonthlyInterestRate, installments);
        ValidatePayrollMargin(monthlySalary, existingPayrollDeductions, provisionalPmt);

        var requiredRole = DetermineRequiredRole(amount, PayrollSupervisorLimit);
        InitializeCommonFields(clientId, amount, installments, MonthlyInterestRate, requiredRole, idempotencyKey);

        EmployerName              = employerName;
        MonthlySalary             = monthlySalary;
        EmploymentStatus          = employmentStatus;
        ExistingPayrollDeductions = existingPayrollDeductions;
    }

    // ── Computed payroll fields (on-the-fly, not persisted) ──────────────────
    public decimal PayrollMarginLimit =>
        Math.Round(MonthlySalary * PayrollMarginPercentage, 2);

    public decimal AvailablePayrollMargin =>
        Math.Round(PayrollMarginLimit - ExistingPayrollDeductions, 2);

    public decimal RemainingPayrollMargin =>
        Math.Round(AvailablePayrollMargin - MonthlyPayment, 2);

    public decimal MarginUsageAfterApproval =>
        PayrollMarginLimit == 0 ? 0m  // guard against division by zero
        : Math.Round(MonthlyPayment / PayrollMarginLimit, 4);

    // ── Validation ───────────────────────────────────────────────────────────
    private static void Validate(
        decimal amount, int installments,
        string employerName, decimal monthlySalary,
        EmploymentStatus employmentStatus)
    {
        if (amount < 1_000m || amount > 80_000m)
            throw new DomainException("Payroll loan amount must be between 1,000 and 80,000.");

        if (installments < 6 || installments > 72)
            throw new DomainException("Payroll loan installments must be between 6 and 72.");

        if (string.IsNullOrWhiteSpace(employerName))
            throw new DomainException("Employer name is required for payroll loans.");

        if (monthlySalary <= 0)
            throw new DomainException("Monthly salary must be greater than zero.");

        if (employmentStatus != EmploymentStatus.Active)
            throw new DomainException("Payroll loans require active employment status.");
    }

    private static void ValidatePayrollMargin(
        decimal monthlySalary, decimal existingDeductions, decimal monthlyPayment)
    {
        var marginLimit     = Math.Round(monthlySalary * PayrollMarginPercentage, 2);
        var availableMargin = Math.Round(marginLimit - existingDeductions, 2);

        if (availableMargin <= 0)
            throw new DomainException(
                "No available payroll margin. Existing deductions exceed the consignable limit.");

        if (monthlyPayment > availableMargin)
            throw new DomainException(
                $"Monthly payment of {monthlyPayment:C} exceeds the available payroll margin of {availableMargin:C}.");
    }
}
