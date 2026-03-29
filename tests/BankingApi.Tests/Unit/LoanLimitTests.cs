using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class LoanLimitTests
{
    [Fact]
    public void PersonalLoan_SmallAmount_RequiresManagerApproval()
    {
        var loan = new PersonalLoan("client-1", 15_000m, 12);
        loan.RequiredApprovalRole.Should().Be(Loan.RoleManager);
    }

    [Fact]
    public void PersonalLoan_MediumAmount_RequiresSupervisorApproval()
    {
        var loan = new PersonalLoan("client-1", 80_000m, 24);
        loan.RequiredApprovalRole.Should().Be(Loan.RoleSupervisor);
    }

    [Fact]
    public void PayrollLoan_SmallAmount_RequiresManagerApproval()
    {
        var loan = new PayrollLoan("client-1", 10_000m, 12, "Corp", 5_000m, 
            EmploymentStatus.Active, 0);
        loan.RequiredApprovalRole.Should().Be(Loan.RoleManager);
    }
}
