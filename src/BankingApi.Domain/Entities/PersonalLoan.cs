using BankingApi.Domain.Exceptions;

namespace BankingApi.Domain.Entities;

public class PersonalLoan : Loan
{
    private const decimal MonthlyInterestRate = 0.015m;  // 1.5%
    private const decimal PersonalSupervisorLimit = 100_000m;

    // ── EF Core constructor ──────────────────────────────────────────────────
    private PersonalLoan() { }

    // ── Factory constructor ──────────────────────────────────────────────────
    public PersonalLoan(string clientId, decimal amount, int installments)
    {
        Validate(amount, installments);

        var requiredRole = DetermineRequiredRole(amount, PersonalSupervisorLimit);
        InitializeCommonFields(clientId, amount, installments, MonthlyInterestRate, requiredRole);
    }

    // ── Validation ───────────────────────────────────────────────────────────
    private static void Validate(decimal amount, int installments)
    {
        if (amount < 1_000m || amount > 200_000m)
            throw new DomainException("Loan amount must be between 1,000 and 200,000.");

        if (installments < 1 || installments > 48)
            throw new DomainException("Installments must be between 1 and 48.");
    }
}
