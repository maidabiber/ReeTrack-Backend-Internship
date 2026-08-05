using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Reports;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// <c>/run</c> must always recompute (the builder's Refresh button depends on this), while
/// derived operations such as export should reuse a run the caller already saw.
/// <see cref="CustomReportService.RunAsync"/> and <see cref="CustomReportService.GetOrRunAsync"/>
/// are the two halves of that contract.
/// </summary>
public class CustomReportServiceRunCacheTests
{
    [Fact]
    public async Task GetOrRunAsync_SecondIdenticalCall_ReturnsTheCachedInstance()
    {
        var service = CreateService(Guid.NewGuid());
        var spec = ValidSpec();

        var first = await service.GetOrRunAsync(spec);
        var second = await service.GetOrRunAsync(spec);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task RunAsync_CalledTwice_AlwaysRecomputes()
    {
        // /run's own contract: the Refresh button relies on RunAsync never answering from the
        // cache, even for back-to-back identical calls.
        var service = CreateService(Guid.NewGuid());
        var spec = ValidSpec();

        var first = await service.RunAsync(spec);
        var second = await service.RunAsync(spec);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task GetOrRunAsync_AfterRunAsync_ReusesTheRunThatWasAlreadyPaidFor()
    {
        // This is the export path: RunAsync (e.g. from /run, shown on screen) writes to the
        // cache; a subsequent GetOrRunAsync (e.g. from Export) reuses it instead of recomputing.
        var service = CreateService(Guid.NewGuid());
        var spec = ValidSpec();

        var shownOnScreen = await service.RunAsync(spec);
        var exported = await service.GetOrRunAsync(spec);

        Assert.Same(shownOnScreen, exported);
    }

    [Fact]
    public async Task GetOrRunAsync_TwoDifferentUsers_DoNotShareTheCachedRun()
    {
        // Both services share one CustomReportRunCache (a singleton in production) but have
        // different current users, so the cache key must include user id, not just the spec.
        var runCache = new CustomReportRunCache();
        var serviceA = CreateService(Guid.NewGuid(), runCache);
        var serviceB = CreateService(Guid.NewGuid(), runCache);
        var spec = ValidSpec();

        var forA = await serviceA.GetOrRunAsync(spec);
        var forB = await serviceB.GetOrRunAsync(spec);

        Assert.NotSame(forA, forB);
    }

    private static CustomReportService CreateService(Guid userId, CustomReportRunCache? runCache = null)
    {
        var db = CreateDb();
        var currentUser = new FakeCurrentUser(userId);
        var pipeline = new ReportEntryPipeline(
            db,
            new FakeRateMultiplierConfigProvider(),
            currentUser,
            Options.Create(new ReportOptions()));

        return new CustomReportService(
            pipeline,
            new FakeProjectCostCalculator(),
            db,
            currentUser,
            writers: [],
            runCache ?? new CustomReportRunCache());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"custom-report-run-cache-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static CustomReportSpec ValidSpec() =>
        new()
        {
            Query = new ReportQuery(),
            Blocks = [new KpiBlockSpec { Id = "b1", Metrics = ["totalHours"] }]
        };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid UserId => userId;
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRateMultiplierConfigProvider : IRateMultiplierConfigProvider
    {
        public Task<RateMultiplierConfig> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(RateMultiplierConfig.Defaults);
    }

    /// <summary>Never invoked: the spec under test only asks for "totalHours", which needs
    /// neither cost nor project data, so <see cref="CustomReportContext"/> should not reach
    /// into the calculator. Throwing makes it loud if that assumption ever breaks.</summary>
    private sealed class FakeProjectCostCalculator : IProjectCostCalculator
    {
        public ProjectCostResult Calculate(
            Project project,
            IReadOnlyList<TimeEntry> projectEntries,
            IReadOnlyList<TimeEntry> crossProjectUserEntries,
            IReadOnlyList<UserHourlyRate> userRates,
            IReadOnlySet<DateOnly> holidays,
            RateMultiplierConfig multiplierConfig) =>
            throw new NotSupportedException("Not needed for a totalHours-only spec.");

        public IReadOnlyList<EntryCostLine> CalculateEntries(
            IReadOnlyList<TimeEntry> entries,
            IReadOnlyList<TimeEntry> crossProjectUserEntries,
            IReadOnlyList<UserHourlyRate> userRates,
            IReadOnlySet<DateOnly> holidays,
            RateMultiplierConfig multiplierConfig) =>
            throw new NotSupportedException("Not needed for a totalHours-only spec.");
    }
}
