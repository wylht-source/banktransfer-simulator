using BankingApi.Application.Common;
using FluentAssertions;

namespace BankingApi.Tests.Unit;

public class PagedResultTests
{
    [Fact]
    public void Create_ValidParams_CreatesSuccessfully()
    {
        var items = new[] { "item1", "item2" };
        var result = new PagedResult<string>(items, 1, 10, 2);

        result.Items.Should().Equal(items);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void Create_Empty_CreatesSuccessfully()
    {
        var result = new PagedResult<int>(Array.Empty<int>(), 1, 10, 0);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void HasNext_WhenNotLastPage_ReturnsTrue()
    {
        var result = new PagedResult<int>(new[] { 1 }, 1, 10, 20);
        result.HasNext.Should().BeTrue();
    }

    [Fact]
    public void HasPrevious_OnFirstPage_ReturnsFalse()
    {
        var result = new PagedResult<int>(new[] { 1 }, 1, 10, 20);
        result.HasPrevious.Should().BeFalse();
    }
}
