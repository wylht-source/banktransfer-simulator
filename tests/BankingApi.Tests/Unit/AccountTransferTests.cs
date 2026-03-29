using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class AccountTransferTests
{
    [Fact]
    public void Debit_WithSufficientFunds_DecrementsBalance()
    {
        var from = Account.Create("Alice", "user-1");
        var to = Account.Create("Bob", "user-2");
        
        from.Deposit(1000m, "Setup");
        from.Debit(300m, "Payment", to.Id);

        from.Balance.Should().Be(700m);
        from.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public void Credit_IncreasesBalance()
    {
        var account = Account.Create("Bob", "user-2");
        var fromId = Guid.NewGuid();
        
        account.Credit(300m, "Received", fromId);
        
        account.Balance.Should().Be(300m);
        account.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void Debit_InsufficientFunds_ThrowsException()
    {
        var account = Account.Create("Alice", "user-1");
        account.Deposit(100m, "Setup");

        var act = () => account.Debit(500m, "Payment", Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }
}
