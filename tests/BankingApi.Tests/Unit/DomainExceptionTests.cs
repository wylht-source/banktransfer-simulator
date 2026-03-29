using BankingApi.Domain.Exceptions;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class DomainExceptionTests
{
    [Fact]
    public void CreateException_WithMessage_StoresMessage()
    {
        var ex = new DomainException("Test error");
        ex.Message.Should().Be("Test error");
    }

    [Fact]
    public void ThrowException_IsException()
    {
        Action act = () => throw new DomainException("Failed");
        act.Should().Throw<DomainException>().WithMessage("Failed");
    }
}
