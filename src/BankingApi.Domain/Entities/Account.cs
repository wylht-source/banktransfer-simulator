using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Domain.Entities;

public class Account
{
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty; // JWT user ID
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    private Account() { } // EF Core

    public static Account Create(string ownerName, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            throw new DomainException("Owner name is required.");

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new DomainException("Owner ID is required.");

        return new Account
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateAccountNumber(),
            OwnerName = ownerName.Trim(),
            OwnerId = ownerId,
            Balance = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deposit(decimal amount, string description, Guid? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new DomainException("Deposit amount must be greater than zero.");

        Balance += amount;
        _transactions.Add(Transaction.Create(
            accountId: Id,
            type: TransactionType.Deposit,
            amount: amount,
            description: description,
            idempotencyKey: idempotencyKey
        ));
    }

    public void Withdraw(decimal amount, string description, Guid? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new DomainException("Withdrawal amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient funds.");

        Balance -= amount;
        _transactions.Add(Transaction.Create(
            accountId: Id,
            type: TransactionType.Withdrawal,
            amount: amount,
            description: description,
            idempotencyKey: idempotencyKey
        ));
    }

    public void Debit(decimal amount, string description, Guid toAccountId, Guid? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new DomainException("Transfer amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient funds for transfer.");

        Balance -= amount;
        _transactions.Add(Transaction.Create(
            accountId: Id,
            type: TransactionType.TransferOut,
            amount: amount,
            description: description,
            idempotencyKey: idempotencyKey,
            fromAccountId: Id,
            toAccountId: toAccountId
        ));
    }

    public void Credit(decimal amount, string description, Guid fromAccountId, Guid? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new DomainException("Transfer amount must be greater than zero.");

        Balance += amount;
        _transactions.Add(Transaction.Create(
            accountId: Id,
            type: TransactionType.TransferIn,
            amount: amount,
            description: description,
            idempotencyKey: idempotencyKey,
            fromAccountId: fromAccountId,
            toAccountId: Id
        ));
    }

    private static string GenerateAccountNumber()
        => $"ACC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
}