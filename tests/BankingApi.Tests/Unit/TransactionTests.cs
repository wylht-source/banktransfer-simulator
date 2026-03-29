using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class TransactionTests
{
    [Fact]
    public void Create_ValidTransaction_CreatesSuccessfully()
    {
        var accountId = Guid.NewGuid();
        var transaction = Transaction.Create(accountId, TransactionType.Deposit, 100m, "Test");

        transaction.AccountId.Should().Be(accountId);
        transaction.Type.Should().Be(TransactionType.Deposit);
        transaction.Amount.Should().Be(100m);
        transaction.Description.Should().Be("Test");
    }

    [Fact]
    public void Create_WithIdempotencyKey_StoresKey()
    {
        var idempotencyKey = Guid.NewGuid();
        var transaction = Transaction.Create(Guid.NewGuid(), TransactionType.Deposit, 50m, "Desc", idempotencyKey);

        transaction.IdempotencyKey.Should().Be(idempotencyKey);
    }

    [Fact]
    public void Create_Transfer_StoresTransferMetadata()
    {
        var fromAccount = Guid.NewGuid();
        var toAccount = Guid.NewGuid();
        var transaction = Transaction.Create(
            fromAccount, TransactionType.TransferOut, 100m, "Transfer", null, fromAccount, toAccount);

        transaction.FromAccountId.Should().Be(fromAccount);
        transaction.ToAccountId.Should().Be(toAccount);
    }
}
