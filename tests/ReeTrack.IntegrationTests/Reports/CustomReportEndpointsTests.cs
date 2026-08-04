using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class CustomReportEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task GetCatalogue_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/custom/catalogue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCatalogue_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/custom/catalogue");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCatalogue_AsAdmin_ReturnsDimensionsAndMetrics()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/custom/catalogue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.True(root.GetProperty("dimensions").GetArrayLength() >= 11);
        Assert.True(root.GetProperty("metrics").GetArrayLength() >= 10);
        Assert.Contains(
            root.GetProperty("metrics").EnumerateArray(),
            m => m.GetProperty("id").GetString() == "revenue"
                 && m.GetProperty("compatibleDimensions").EnumerateArray()
                     .Any(d => d.GetString() == "project"));
    }

    [Fact]
    public async Task Run_AsAdmin_ReturnsKpiAndBreakdownBlocks()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var (member, _) = await factory.SeedMemberAsync("custom-alice@reetrack.test", "Alice");
        var client = factory.CreateAuthenticatedClient(adminToken);

        var clientId = await SeedClientAsync(factory, "CustomAcme");
        var projectId = await SeedProjectAsync(factory, clientId, "CustomAlpha", hourlyRate: 50m);

        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await SeedEntryAsync(factory, member.Id, projectId, monday.AddHours(9), 7200, isBillable: true);
        await SeedEntryAsync(factory, admin.Id, projectId, monday.AddHours(13), 3600, isBillable: false);

        var body = new
        {
            spec = new
            {
                version = 1,
                query = new { },
                blocks = new object[]
                {
                    new
                    {
                        type = "kpi",
                        id = "b1",
                        metrics = new[] { "totalHours", "billablePct", "entryCount" }
                    },
                    new
                    {
                        type = "breakdown",
                        id = "b2",
                        title = "By client",
                        dimensions = new[] { "client" },
                        metrics = new[] { "totalHours", "labourCost" },
                        computed = new[]
                        {
                            new
                            {
                                id = "c1",
                                label = "Effective rate",
                                left = "labourCost",
                                @operator = "Divide",
                                right = "totalHours"
                            }
                        },
                        sortKey = "totalHours",
                        sortDescending = true,
                        showTotals = true
                    },
                    new
                    {
                        type = "chart",
                        id = "b3",
                        dimension = "week",
                        metrics = new[] { "totalHours" },
                        kind = "Area"
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/reports/custom/run", body, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(10800, root.GetProperty("kpis").GetProperty("totalSeconds").GetInt64());
        Assert.Equal(3, root.GetProperty("blocks").GetArrayLength());

        var kpi = root.GetProperty("blocks")[0];
        Assert.Equal("kpi", kpi.GetProperty("type").GetString());
        Assert.Equal(3, kpi.GetProperty("cells").GetArrayLength());

        var table = root.GetProperty("blocks")[1];
        Assert.Equal("table", table.GetProperty("type").GetString());
        Assert.True(table.GetProperty("rows").GetArrayLength() >= 1);

        var series = root.GetProperty("blocks")[2];
        Assert.Equal("series", series.GetProperty("type").GetString());
        Assert.Equal("Area", series.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Run_IncompatibleMetric_ReturnsValidation()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var body = new
        {
            spec = new
            {
                version = 1,
                query = new { },
                blocks = new object[]
                {
                    new
                    {
                        type = "breakdown",
                        id = "b1",
                        dimensions = new[] { "day" },
                        metrics = new[] { "revenue" }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/reports/custom/run", body, JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Run_OpenEndedQuery_IncludesOldEntries()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var clientId = await SeedClientAsync(factory, "LifetimeAcme");
        var projectId = await SeedProjectAsync(factory, clientId, "LifetimeAlpha", hourlyRate: 40m);

        var old = new DateTime(2020, 1, 6, 9, 0, 0, DateTimeKind.Utc);
        await SeedEntryAsync(factory, admin.Id, projectId, old, 3600, isBillable: true);
        await SeedEntryAsync(
            factory,
            admin.Id,
            projectId,
            CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(10),
            3600,
            isBillable: true);

        var body = new
        {
            spec = new
            {
                version = 1,
                query = new { },
                blocks = new object[]
                {
                    new { type = "kpi", id = "b1", metrics = new[] { "totalHours", "entryCount" } }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/reports/custom/run", body, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(7200, root.GetProperty("kpis").GetProperty("totalSeconds").GetInt64());
        Assert.Equal("2020-01-06", root.GetProperty("firstEntryDate").GetString());
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
        decimal hourlyRate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new Project
        {
            ClientId = clientId,
            Name = name,
            Status = ProjectStatus.Active,
            HourlyRate = hourlyRate,
            CurrencyCode = "EUR"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static async Task SeedEntryAsync(
        ReeTrackWebApplicationFactory factory,
        Guid userId,
        Guid projectId,
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
}
