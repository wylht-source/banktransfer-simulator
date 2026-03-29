using BankingApi.Domain.Entities;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class AccountNumberGenerationTests
{
    [Fact]
    public void Create_GeneratesUniqueAccountNumbers()
    {
        var account1 = Account.Create("User1", "id1");
        var account2 = Account.Create("User2", "id2");

        account1.AccountNumber.Should().NotBe(account2.AccountNumber);
    }

    [Fact]
    public void Create_AccountNumberStartsWithPrefix()
    {
        var account = Account.Create("User", "id");
        account.AccountNumber.Should().StartWith("ACC-");
    }

    [Fact]
    public void Create_AccountNumberIsNotEmpty()
    {
        var account = Account.Create("User", "id");
        account.AccountNumber.Should().NotBeNullOrEmpty();
    }
}
