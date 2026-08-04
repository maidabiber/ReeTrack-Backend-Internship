using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class MetricCompatibilityTests
{
    [Fact]
    public void ProjectScopedMetric_OnDayDimension_IsRejected()
    {
        var metric = MetricCatalog.GetRequired("revenue");
        Assert.False(MetricCompatibility.IsValid(metric, ["day"]));
    }

    [Fact]
    public void ProjectScopedMetric_OnProjectDimension_IsAllowed()
    {
        var metric = MetricCatalog.GetRequired("revenue");
        Assert.True(MetricCompatibility.IsValid(metric, ["project"]));
        Assert.True(MetricCompatibility.IsValid(metric, ["client"]));
    }

    [Fact]
    public void UserScopedMetric_OnlyAllowsUserDimension()
    {
        var metric = MetricCatalog.GetRequired("capacityUtilizationPct");
        Assert.True(MetricCompatibility.IsValid(metric, ["user"]));
        Assert.False(MetricCompatibility.IsValid(metric, ["project"]));
    }

    [Fact]
    public void EntryScopedMetric_AllowsAnyDimension()
    {
        var metric = MetricCatalog.GetRequired("totalHours");
        Assert.True(MetricCompatibility.IsValid(metric, ["tag"]));
        Assert.True(MetricCompatibility.IsValid(metric, ["day", "user"]));
    }
}
