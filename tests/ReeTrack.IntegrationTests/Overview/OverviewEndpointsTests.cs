using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Overview;

public class OverviewEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetOverview_NonAdmin_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOverview_Admin_ReturnsKpisActiveTimerAndIdleMembers()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var start = await adminClient.PostAsJsonAsync("/api/time-entries/timer/start", new
        {
            description = "Overview live work"
        });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var running = await start.Content.ReadFromJsonAsync<TimerStartResponse>(JsonOptions);
        Assert.NotNull(running);

        var response = await adminClient.GetAsync("/api/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var overview = await response.Content.ReadFromJsonAsync<AdminOverviewResponse>(JsonOptions);
        Assert.NotNull(overview);
        Assert.Equal(1, overview!.OnTheClock);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), overview.Today.Date);

        var timer = Assert.Single(overview.ActiveTimers);
        Assert.Equal(running!.Id, timer.TimeEntryId);
        Assert.Equal(admin.Id, timer.UserId);
        Assert.Equal("Overview live work", timer.Description);
        Assert.True(timer.IsUnassigned);
        Assert.False(timer.IsStale);

        // Admin is on the clock; member has no logged time → idle.
        Assert.Contains(overview.IdleMembers, m => m.UserId == member.Id);
        Assert.DoesNotContain(overview.IdleMembers, m => m.UserId == admin.Id);
        Assert.True(overview.IdleCount >= 1);
    }

    [Fact]
    public async Task GetOverview_StoppedTimer_MovesIntoTodayKpis()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var start = await adminClient.PostAsJsonAsync("/api/time-entries/timer/start", new
        {
            description = "Finishing soon"
        });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        await Task.Delay(1100);

        var stop = await adminClient.PostAsJsonAsync("/api/time-entries/timer/stop", new
        {
            description = "Finishing soon"
        });
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);

        var overview = await adminClient.GetFromJsonAsync<AdminOverviewResponse>(
            "/api/overview", JsonOptions);

        Assert.NotNull(overview);
        Assert.Equal(0, overview!.OnTheClock);
        Assert.Empty(overview.ActiveTimers);
        Assert.True(overview.Today.TotalSeconds >= 1);
        Assert.Equal(1, overview.Today.EntryCount);
        Assert.Equal(1, overview.Today.MembersLogged);
    }

    private sealed class TimerStartResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class AdminOverviewResponse
    {
        public DateTime GeneratedAtUtc { get; set; }
        public OverviewTodayKpisResponse Today { get; set; } = new();
        public int OnTheClock { get; set; }
        public List<ActiveTimerOverviewResponse> ActiveTimers { get; set; } = [];
        public List<IdleMemberOverviewResponse> IdleMembers { get; set; } = [];
        public int IdleCount { get; set; }
        public List<OverviewProjectHoursResponse> TopProjects { get; set; } = [];
    }

    private sealed class OverviewTodayKpisResponse
    {
        public DateOnly Date { get; set; }
        public long TotalSeconds { get; set; }
        public long BillableSeconds { get; set; }
        public decimal BillablePct { get; set; }
        public int EntryCount { get; set; }
        public int MembersLogged { get; set; }
        public long UnassignedSeconds { get; set; }
    }

    private sealed class ActiveTimerOverviewResponse
    {
        public Guid TimeEntryId { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public string? Description { get; set; }
        public bool IsBillable { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public bool IsUnassigned { get; set; }
        public bool IsStale { get; set; }
    }

    private sealed class IdleMemberOverviewResponse
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? AvatarUrl { get; set; }
    }

    private sealed class OverviewProjectHoursResponse
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = "";
        public long TotalSeconds { get; set; }
    }
}
