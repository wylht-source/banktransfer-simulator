using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class PersonalLoanTests
{
    [Fact]
    public void Create_ValidParams_CreatesSuccessfully()
    {
        var loan = new PersonalLoan("client-1", 50_000m, 24);

        loan.ClientId.Should().Be("client-1");
        loan.Amount.Should().Be(50_000m);
        loan.Installments.Should().Be(24);
    }

    [Fact]
    public void Create_AmountTooLow_ThrowsException()
    {
        var act = () => new PersonalLoan("client-1", 500m, 12);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_AmountTooHigh_ThrowsException()
    {
        var act = () => new PersonalLoan("client-1", 300_000m, 12);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_InvalidInstallments_ThrowsException()
    {
        var act = () => new PersonalLoan("client-1", 50_000m, 60);
        act.Should().Throw<DomainException>();
    }
}
