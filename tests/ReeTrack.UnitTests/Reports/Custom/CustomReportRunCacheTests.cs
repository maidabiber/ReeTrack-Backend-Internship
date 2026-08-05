using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// The run cache is keyed on (user id, cache key) — see the "Two different user ids" case,
/// which pins the reason a shared key would be wrong: <see cref="CustomReportDto.GeneratedByName"/>
/// is attributed to whoever ran the report, so a cross-user hit would stamp one admin's name on
/// another admin's export.
/// </summary>
public class CustomReportRunCacheTests
{
    [Fact]
    public void TryGet_BeforeAnySet_Misses()
    {
        var cache = new CustomReportRunCache();

        var hit = cache.TryGet(Guid.NewGuid(), "key", out _);

        Assert.False(hit);
    }

    [Fact]
    public void Set_ThenTryGet_WithSameUserAndKey_ReturnsTheSameInstance()
    {
        var cache = new CustomReportRunCache();
        var userId = Guid.NewGuid();
        var report = SampleReport();

        cache.Set(userId, "key", report);
        var hit = cache.TryGet(userId, "key", out var cached);

        Assert.True(hit);
        Assert.Same(report, cached);
    }

    [Fact]
    public void TryGet_WithADifferentUserId_DoesNotShareTheEntry()
    {
        var cache = new CustomReportRunCache();
        var owner = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var report = SampleReport();

        cache.Set(owner, "key", report);

        Assert.True(cache.TryGet(owner, "key", out _));
        Assert.False(cache.TryGet(otherUser, "key", out _));
    }

    [Fact]
    public void TryGet_WithADifferentCacheKey_Misses()
    {
        var cache = new CustomReportRunCache();
        var userId = Guid.NewGuid();

        cache.Set(userId, "key-a", SampleReport());

        Assert.False(cache.TryGet(userId, "key-b", out _));
    }

    private static CustomReportDto SampleReport() => new()
    {
        Kpis = new ReportKpisDto
        {
            TotalSeconds = 0,
            BillableSeconds = 0,
            NonBillableSeconds = 0,
            BillablePct = 0,
            EntryCount = 0,
            ActiveMembers = 0,
            ActiveProjects = 0,
            OvertimeHours = 0,
            WeekendHours = 0,
            HolidayHours = 0,
            UnassignedSeconds = 0
        },
        Basis = new ReportBasisDto
        {
            WeekendPremium = 0.5m,
            HolidayPremium = 1.0m,
            OvertimePremium = 0.5m,
            WeeklyOvertimeThresholdHours = 40m
        },
        GeneratedAtUtc = DateTime.UtcNow,
        Blocks = []
    };
}
