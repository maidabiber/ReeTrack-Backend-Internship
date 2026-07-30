using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class ReportDetailedEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task GetDetailed_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/detailed");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDetailed_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/detailed");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDetailed_AsAdmin_ReturnsPagedEntriesAndKpis()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var project = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Alpha",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            for (var i = 0; i < 3; i++)
            {
                var started = monday.AddDays(i).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
                db.TimeEntries.Add(new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = project.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started,
                    EndedAtUtc = started.AddHours(1),
                    DurationSeconds = 3600,
                    Status = TimeEntryStatus.Confirmed,
                    Description = $"Day {i}"
                });
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/reports/detailed?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detailed = await response.Content.ReadFromJsonAsync<DetailedResponse>();
        Assert.NotNull(detailed);
        Assert.Equal(3, detailed.Kpis.EntryCount);
        Assert.Equal(10800, detailed.Kpis.TotalSeconds);
        Assert.Equal(3, detailed.TotalCount);
        Assert.Equal(1, detailed.Page);
        Assert.Equal(2, detailed.PageSize);
        Assert.Equal(2, detailed.Entries.Count);
        Assert.All(detailed.Entries, entry => Assert.Equal("Alpha", entry.ProjectName));
    }

    [Fact]
    public async Task GetDetailed_WithProjectFilter_ReturnsOnlyMatchingRows()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek;

        Guid alphaId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var alpha = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Alpha",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            var beta = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Beta",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            db.Projects.AddRange(alpha, beta);
            await db.SaveChangesAsync();

            var started = monday.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
            db.TimeEntries.AddRange(
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = alpha.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started,
                    EndedAtUtc = started.AddHours(1),
                    DurationSeconds = 3600,
                    Status = TimeEntryStatus.Confirmed
                },
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = beta.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started.AddHours(2),
                    EndedAtUtc = started.AddHours(3),
                    DurationSeconds = 3600,
                    Status = TimeEntryStatus.Confirmed
                });
            await db.SaveChangesAsync();
            alphaId = alpha.Id;
        }

        var detailed = await client.GetFromJsonAsync<DetailedResponse>(
            $"/api/reports/detailed?projectIds={alphaId}");

        Assert.NotNull(detailed);
        Assert.Equal(1, detailed.Kpis.EntryCount);
        Assert.Equal("Alpha", Assert.Single(detailed.Entries).ProjectName);
    }

    [Theory]
    [InlineData("csv", "text/csv", new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { (byte)'P', (byte)'K' })]
    [InlineData("pdf", "application/pdf", new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' })]
    public async Task ExportDetailed_AsAdmin_ReturnsFile(
        string format,
        string contentType,
        byte[] magic)
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();
            var project = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Alpha",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            db.TimeEntries.Add(new TimeEntry
            {
                UserId = admin.Id,
                ClientId = customer.Id,
                ProjectId = project.Id,
                IsBillable = true,
                Mode = TimeEntryMode.Manual,
                StartedAtUtc = monday.AddHours(9),
                EndedAtUtc = monday.AddHours(10),
                DurationSeconds = 3600,
                Status = TimeEntryStatus.Confirmed
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/reports/detailed/export?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("reetrack-detailed_", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(magic, bytes.Take(magic.Length).ToArray());
    }

    [Fact]
    public async Task ExportDetailed_WithGroupByProject_IncludesGroupHeadersInCsv()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = new Client { Name = "Acme" };
            db.Clients.Add(customer);
            await db.SaveChangesAsync();

            var alpha = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Alpha",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            var beta = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Beta",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 50m
            };
            db.Projects.AddRange(alpha, beta);
            await db.SaveChangesAsync();

            db.TimeEntries.AddRange(
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = alpha.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = monday.AddHours(9),
                    EndedAtUtc = monday.AddHours(10),
                    DurationSeconds = 3600,
                    Status = TimeEntryStatus.Confirmed
                },
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = beta.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = monday.AddHours(11),
                    EndedAtUtc = monday.AddHours(13),
                    DurationSeconds = 7200,
                    Status = TimeEntryStatus.Confirmed
                });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/reports/detailed/export?format=csv&groupBy=project");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Group,Alpha · 1 entries · 1h,", csv);
        Assert.Contains("Group,Beta · 1 entries · 2h,", csv);

        var alphaHeader = csv.IndexOf("Group,Alpha · 1 entries · 1h,", StringComparison.Ordinal);
        var betaHeader = csv.IndexOf("Group,Beta · 1 entries · 2h,", StringComparison.Ordinal);
        Assert.True(alphaHeader >= 0);
        Assert.True(betaHeader > alphaHeader);
    }

    private sealed class DetailedResponse
    {
        public KpisResponse Kpis { get; init; } = new();
        public IReadOnlyList<EntryResponse> Entries { get; init; } = [];
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
    }

    private sealed class KpisResponse
    {
        public int EntryCount { get; init; }
        public long TotalSeconds { get; init; }
    }

    private sealed class EntryResponse
    {
        public string? ProjectName { get; init; }
    }
}
