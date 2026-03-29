using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class AccountTests
{
    private const string DefaultOwnerId = "user-123";

    [Fact]
    public void Create_ValidOwner_CreatesAccountWithZeroBalance()
    {
        var account = Account.Create("Sergio Yamamoto", DefaultOwnerId);

        account.Balance.Should().Be(0);
        account.OwnerName.Should().Be("Sergio Yamamoto");
        account.OwnerId.Should().Be(DefaultOwnerId);
        account.AccountNumber.Should().StartWith("ACC-");
    }

    [Fact]
    public void Create_EmptyOwner_ThrowsDomainException()
    {
        var act = () => Account.Create("", DefaultOwnerId);

        act.Should().Throw<DomainException>().WithMessage("*required*");
    }

    [Fact]
    public void Create_EmptyOwnerId_ThrowsDomainException()
    {
        var act = () => Account.Create("Sergio", "");

        act.Should().Throw<DomainException>().WithMessage("*required*");
    }

    [Fact]
    public void Deposit_ValidAmount_IncreasesBalance()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(1000m, "Salary");

        account.Balance.Should().Be(1000m);
    }

    [Fact]
    public void Deposit_CreatesTransactionRecord()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(500m, "Bonus");

        account.Transactions.Should().HaveCount(1);
        account.Transactions.First().Amount.Should().Be(500m);
    }

    [Fact]
    public void Deposit_NegativeAmount_ThrowsDomainException()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);

        var act = () => account.Deposit(-100m, "Invalid");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Withdraw_ZeroAmount_ThrowsDomainException()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(500m, "Setup");

        var act = () => account.Withdraw(0m, "Zero");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Withdraw_WithIdempotencyKey_StoresKeyOnTransaction()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(500m, "Setup");
        var key = Guid.NewGuid();

        account.Withdraw(100m, "Rent", idempotencyKey: key);

        account.Transactions.Last().IdempotencyKey.Should().Be(key);
    }

    [Fact]
    public void Withdraw_SufficientFunds_DecreasesBalance()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(1000m, "Setup");

        account.Withdraw(300m, "Rent");

        account.Balance.Should().Be(700m);
    }

    [Fact]
    public void Withdraw_InsufficientFunds_ThrowsDomainException()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        account.Deposit(100m, "Setup");

        var act = () => account.Withdraw(500m, "Overdraft");

        act.Should().Throw<DomainException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public void Debit_And_Credit_SimulateTransfer()
    {
        var from = Account.Create("Sergio", DefaultOwnerId);
        var to = Account.Create("Tanaka", "user-456");
        from.Deposit(1000m, "Setup");

        from.Debit(400m, "Transfer to Tanaka", toAccountId: to.Id);
        to.Credit(400m, "Transfer from Sergio", fromAccountId: from.Id);

        from.Balance.Should().Be(600m);
        to.Balance.Should().Be(400m);
    }

    [Fact]
    public void Deposit_WithIdempotencyKey_StoresKeyOnTransaction()
    {
        var account = Account.Create("Sergio", DefaultOwnerId);
        var key = Guid.NewGuid();

        account.Deposit(100m, "Test", idempotencyKey: key);

        account.Transactions.First().IdempotencyKey.Should().Be(key);
    }
}