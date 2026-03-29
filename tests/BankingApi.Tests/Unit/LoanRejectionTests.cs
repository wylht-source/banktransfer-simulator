using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class LoanRejectionTests
{
    [Fact]
    public void Reject_ValidReason_UpdatesStatus()
    {
        var loan = new PersonalLoan("client-1", 15_000m, 12);  // Small amount = Manager only
        loan.Reject("manager-1", Loan.RoleManager, "Insufficient income");

        loan.Status.Should().Be(LoanStatus.Rejected);
        loan.RejectionReason.Should().Contain("Insufficient income");
    }

    [Fact]
    public void Reject_EmptyReason_ThrowsException()
    {
        var loan = new PersonalLoan("client-1", 50_000m, 24);
        var act = () => loan.Reject("manager-1", Loan.RoleManager, "");

        act.Should().Throw<DomainException>();
    }
}
