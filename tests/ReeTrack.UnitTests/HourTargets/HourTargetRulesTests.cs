using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.Services;
using Xunit;

namespace ReeTrack.UnitTests.HourTargets;

public class HourTargetRulesTests
{
    [Theory]
    [InlineData(HourTargetMode.Daily, 0)]
    [InlineData(HourTargetMode.Daily, -1)]
    [InlineData(HourTargetMode.Weekly, 0)]
    public void EnsureValid_RejectsNonPositiveHours(HourTargetMode mode, decimal hours)
    {
        var ex = Assert.Throws<DomainException>(() => HourTargetRules.EnsureValid(mode, hours));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public void EnsureValid_RejectsDailyAbove24()
    {
        var ex = Assert.Throws<DomainException>(() => HourTargetRules.EnsureValid(HourTargetMode.Daily, 24.01m));
        Assert.Contains("24", ex.Message);
    }

    [Fact]
    public void EnsureValid_RejectsWeeklyAbove168()
    {
        var ex = Assert.Throws<DomainException>(() => HourTargetRules.EnsureValid(HourTargetMode.Weekly, 169m));
        Assert.Contains("168", ex.Message);
    }

    [Theory]
    [InlineData(HourTargetMode.Daily, 8)]
    [InlineData(HourTargetMode.Daily, 24)]
    [InlineData(HourTargetMode.Weekly, 40)]
    [InlineData(HourTargetMode.Weekly, 168)]
    public void EnsureValid_AcceptsBoundaryValues(HourTargetMode mode, decimal hours)
    {
        HourTargetRules.EnsureValid(mode, hours);
    }
}
