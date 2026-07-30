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

public class ReportWorkloadEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task GetWorkload_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/workload");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/workload");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkload_AsAdmin_AllocationsReconcileToTotalSeconds()
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
                    EndedAtUtc = started.AddHours(4),
                    DurationSeconds = 7200,
                    Status = TimeEntryStatus.Confirmed
                });
            await db.SaveChangesAsync();
        }

        var workload = await client.GetFromJsonAsync<WorkloadResponse>("/api/reports/workload");

        Assert.NotNull(workload);
        Assert.Equal(10800, workload.Kpis.TotalSeconds);
        Assert.Equal(10800, workload.GrandTotalSeconds);
        Assert.Equal(2, workload.Allocations.Count);
        Assert.Equal(workload.GrandTotalSeconds, workload.Allocations.Sum(a => a.TotalSeconds));
        Assert.Contains(workload.Allocations, a => a.ProjectName == "Alpha" && a.ClientName == "Acme");
        Assert.Contains(workload.Allocations, a => a.ProjectName == "Beta" && a.TotalSeconds == 7200);
    }

    [Theory]
    [InlineData("csv", "text/csv", new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { (byte)'P', (byte)'K' })]
    [InlineData("pdf", "application/pdf", new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' })]
    public async Task ExportWorkload_AsAdmin_ReturnsFile(
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

        var response = await client.GetAsync($"/api/reports/workload/export?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("reetrack-workload_", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(magic, bytes.Take(magic.Length).ToArray());
    }

    private sealed class WorkloadResponse
    {
        public KpisResponse Kpis { get; init; } = new();
        public IReadOnlyList<AllocationResponse> Allocations { get; init; } = [];
        public long GrandTotalSeconds { get; init; }
    }

    private sealed class KpisResponse
    {
        public long TotalSeconds { get; init; }
    }

    private sealed class AllocationResponse
    {
        public string ClientName { get; init; } = "";
        public string ProjectName { get; init; } = "";
        public long TotalSeconds { get; init; }
    }
}
