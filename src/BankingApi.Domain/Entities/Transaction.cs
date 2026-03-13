using BankingApi.Domain.Enums;

namespace BankingApi.Domain.Entities;

/// <summary>
/// Immutable ledger entry. Never updated, never deleted.
/// </summary>
public class Transaction
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public Guid? IdempotencyKey { get; private set; }

    // Transfer metadata — null for deposits and withdrawals
    public Guid? FromAccountId { get; private set; }
    public Guid? ToAccountId { get; private set; }

    private Transaction() { } // EF Core

    public static Transaction Create(
        Guid accountId,
        TransactionType type,
        decimal amount,
        string description,
        Guid? idempotencyKey = null,
        Guid? fromAccountId = null,
        Guid? toAccountId = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId
        };
    }
}