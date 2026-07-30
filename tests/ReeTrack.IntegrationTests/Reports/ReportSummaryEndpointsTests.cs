using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class ReportSummaryEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task GetSummary_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_AsAdmin_ReturnsPopulatedPortfolio()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync("alice@reetrack.test", "Alice");
        var client = factory.CreateAuthenticatedClient(adminToken);

        var clientId = await SeedClientAsync(factory, "Acme");
        var projectA = await SeedProjectAsync(factory, clientId, "Alpha", hourlyRate: 50m, currencyCode: "EUR", timeEstimateHours: 2m);
        var projectB = await SeedProjectAsync(factory, clientId, "Beta", hourlyRate: 80m, currencyCode: "USD", fixedFeeAmount: 500m);

        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var previousMonday = CurrentWeek.AddDays(-7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await SeedEntryAsync(factory, member.Id, projectA, monday.AddHours(9), durationSeconds: 7200, isBillable: true);
        await SeedEntryAsync(factory, member.Id, projectA, monday.AddDays(5), durationSeconds: 3600, isBillable: false); // Saturday
        await SeedEntryAsync(factory, admin.Id, projectB, previousMonday.AddHours(10), durationSeconds: 3600, isBillable: true);

        var response = await client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SummaryReportResponse>();
        Assert.NotNull(body);

        Assert.Equal(14400, body.Kpis.TotalSeconds);
        Assert.Equal(10800, body.Kpis.BillableSeconds);
        Assert.Equal(3600, body.Kpis.NonBillableSeconds);
        Assert.Equal(75m, body.Kpis.BillablePct);
        Assert.Equal(3, body.Kpis.EntryCount);
        Assert.Equal(2, body.Kpis.ActiveMembers);
        Assert.Equal(2, body.Kpis.ActiveProjects);
        Assert.True(body.Kpis.WeekendHours >= 1m);

        Assert.Equal(7, body.Activity.Count);
        Assert.Equal("Monday", body.Activity[0].DayOfWeek);
        Assert.Equal("Sunday", body.Activity[6].DayOfWeek);
        Assert.Equal(10800, body.Activity[0].TotalSeconds); // Mon: 7200 + 3600
        Assert.Equal(3600, body.Activity[5].TotalSeconds);  // Saturday

        Assert.Equal(26, body.WeeklyTrend.Count);
        Assert.Equal(CurrentWeek, body.WeeklyTrend[^1].WeekStartDate);
        Assert.Equal(10800, body.WeeklyTrend[^1].TotalSeconds);
        Assert.Equal(CurrentWeek.AddDays(-7), body.WeeklyTrend[^2].WeekStartDate);
        Assert.Equal(3600, body.WeeklyTrend[^2].TotalSeconds);

        Assert.Equal(2, body.Projects.Count);
        var alpha = Assert.Single(body.Projects, p => p.Name == "Alpha");
        Assert.Equal("EUR", alpha.CurrencyCode);
        Assert.Equal(10800, alpha.TotalSeconds);
        Assert.True(alpha.CalculatedCost > 0);
        Assert.True(alpha.WeekendHours >= 1m);
        Assert.Equal("Acme", alpha.ClientName);
        Assert.Equal("Active", alpha.Status);
        Assert.Equal(50m, alpha.HourlyRate);
        Assert.Equal(2m, alpha.TimeEstimateHours);

        var beta = Assert.Single(body.Projects, p => p.Name == "Beta");
        Assert.Equal("USD", beta.CurrencyCode);
        Assert.Equal(3600, beta.TotalSeconds);
        Assert.Equal("Acme", beta.ClientName);
        Assert.Equal(500m, beta.FixedFeeAmount);

        Assert.Equal(2, body.Members.Count);
        Assert.Contains(body.Members, m => m.DisplayName == "Alice" && m.TotalSeconds == 10800);
        Assert.Contains(body.Members, m => m.UserId == admin.Id && m.TotalSeconds == 3600);
        Assert.True(body.GeneratedAtUtc <= DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task GetSummary_WithTimeOnNoProject_ReportsItSoProjectRowsReconcile()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(adminToken);

        var clientId = await SeedClientAsync(factory, "Acme");
        var projectId = await SeedProjectAsync(factory, clientId, "Alpha", hourlyRate: 50m);
        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await SeedEntryAsync(factory, admin.Id, projectId, monday.AddHours(9), 7200, isBillable: true);
        // No project — counted in the KPIs but never in the per-project breakdown.
        await SeedEntryAsync(factory, admin.Id, null, monday.AddHours(14), 3600, isBillable: true);

        var body = await client.GetFromJsonAsync<SummaryReportResponse>("/api/reports/summary");
        Assert.NotNull(body);

        Assert.Equal(10800, body.Kpis.TotalSeconds);
        Assert.Equal(3600, body.Kpis.UnassignedSeconds);

        // The whole point: project rows + unassigned == the portfolio total.
        var projectSeconds = body.Projects.Sum(p => p.TotalSeconds);
        Assert.Equal(7200, projectSeconds);
        Assert.Equal(body.Kpis.TotalSeconds, projectSeconds + body.Kpis.UnassignedSeconds);

        Assert.Equal(DateOnly.FromDateTime(monday), body.FirstEntryDate);
        Assert.False(string.IsNullOrWhiteSpace(body.GeneratedByName));
    }

    private static async Task<Guid> SeedClientAsync(ReeTrackWebApplicationFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Client { Name = name };
        db.Clients.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    private static async Task<Guid> SeedProjectAsync(
        ReeTrackWebApplicationFactory factory,
        Guid clientId,
        string name,
        decimal? hourlyRate = null,
        string currencyCode = "EUR",
        decimal? fixedFeeAmount = null,
        decimal? timeEstimateHours = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new Project
        {
            ClientId = clientId,
            Name = name,
            Status = ProjectStatus.Active,
            HourlyRate = hourlyRate,
            CurrencyCode = currencyCode,
            FixedFeeAmount = fixedFeeAmount,
            TimeEstimateHours = timeEstimateHours
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    /// <summary>Pass a null <paramref name="projectId"/> to seed unassigned time.</summary>
    private static async Task SeedEntryAsync(
        ReeTrackWebApplicationFactory factory,
        Guid userId,
        Guid? projectId,
        DateTime startedAtUtc,
        int durationSeconds,
        bool isBillable)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TimeEntries.Add(new TimeEntry
        {
            UserId = userId,
            ProjectId = projectId,
            Mode = TimeEntryMode.Manual,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc.AddSeconds(durationSeconds),
            DurationSeconds = durationSeconds,
            IsBillable = isBillable,
            Status = TimeEntryStatus.Confirmed
        });
        await db.SaveChangesAsync();
    }

    private sealed class SummaryReportResponse
    {
        public ReportKpisResponse Kpis { get; init; } = null!;
        public IReadOnlyList<DayOfWeekHoursResponse> Activity { get; init; } = [];
        public IReadOnlyList<TrendPointResponse> WeeklyTrend { get; init; } = [];
        public IReadOnlyList<ProjectSummaryResponse> Projects { get; init; } = [];
        public IReadOnlyList<MemberHoursResponse> Members { get; init; } = [];
        public DateTime GeneratedAtUtc { get; init; }
        public DateOnly? FirstEntryDate { get; init; }
        public string? GeneratedByName { get; init; }
    }

    private sealed class ReportKpisResponse
    {
        public long TotalSeconds { get; init; }
        public long BillableSeconds { get; init; }
        public long NonBillableSeconds { get; init; }
        public decimal BillablePct { get; init; }
        public int EntryCount { get; init; }
        public int ActiveMembers { get; init; }
        public int ActiveProjects { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal WeekendHours { get; init; }
        public decimal HolidayHours { get; init; }
        public long UnassignedSeconds { get; init; }
    }

    private sealed class DayOfWeekHoursResponse
    {
        public string DayOfWeek { get; init; } = string.Empty;
        public long TotalSeconds { get; init; }
    }

    private sealed class TrendPointResponse
    {
        public DateOnly WeekStartDate { get; init; }
        public long TotalSeconds { get; init; }
    }

    private sealed class ProjectSummaryResponse
    {
        public Guid ProjectId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string CurrencyCode { get; init; } = string.Empty;
        public long TotalSeconds { get; init; }
        public decimal CalculatedCost { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal WeekendHours { get; init; }
        public decimal HolidayHours { get; init; }
        public string ClientName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal? HourlyRate { get; init; }
        public decimal? FixedFeeAmount { get; init; }
        public decimal? TimeEstimateHours { get; init; }
    }

    private sealed class MemberHoursResponse
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public long TotalSeconds { get; init; }
    }
}
