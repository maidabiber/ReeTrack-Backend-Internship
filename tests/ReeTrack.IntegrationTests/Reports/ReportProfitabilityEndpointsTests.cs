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

public class ReportProfitabilityEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task GetProfitability_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/profitability");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfitability_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/profitability");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProfitability_HourlyAndFixedFee_NeverCrossSumsCurrencies()
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

            var hourly = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Hourly",
                Status = ProjectStatus.Active,
                CurrencyCode = "EUR",
                HourlyRate = 80m
            };
            var fixedFee = new Project
            {
                ClientId = customer.Id,
                CreatedByUserId = admin.Id,
                Name = "Fixed",
                Status = ProjectStatus.Active,
                CurrencyCode = "USD",
                FixedFeeAmount = 1000m
            };
            db.Projects.AddRange(hourly, fixedFee);
            await db.SaveChangesAsync();

            var started = monday.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
            db.TimeEntries.AddRange(
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = hourly.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started,
                    EndedAtUtc = started.AddHours(2),
                    DurationSeconds = 7200,
                    Status = TimeEntryStatus.Confirmed
                },
                new TimeEntry
                {
                    UserId = admin.Id,
                    ClientId = customer.Id,
                    ProjectId = fixedFee.Id,
                    IsBillable = true,
                    Mode = TimeEntryMode.Manual,
                    StartedAtUtc = started.AddHours(3),
                    EndedAtUtc = started.AddHours(4),
                    DurationSeconds = 3600,
                    Status = TimeEntryStatus.Confirmed
                });
            await db.SaveChangesAsync();
        }

        var report = await client.GetFromJsonAsync<ProfitabilityResponse>("/api/reports/profitability");

        Assert.NotNull(report);
        Assert.Equal(2, report.ByCurrency.Count);
        Assert.DoesNotContain(report.ByCurrency, c => c.CurrencyCode is not ("EUR" or "USD" or "—"));

        var eur = Assert.Single(report.ByCurrency, c => c.CurrencyCode == "EUR");
        Assert.Equal(160m, eur.Revenue); // 2h × 80

        var usd = Assert.Single(report.ByCurrency, c => c.CurrencyCode == "USD");
        Assert.Equal(1000m, usd.Revenue); // full fixed fee

        var fixedProject = Assert.Single(report.Projects, p => p.Name == "Fixed");
        Assert.Equal("FixedFee", fixedProject.BillingModel);
        Assert.Equal(1000m, fixedProject.Revenue);
    }

    [Theory]
    [InlineData("csv", "text/csv", new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { (byte)'P', (byte)'K' })]
    [InlineData("pdf", "application/pdf", new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' })]
    public async Task ExportProfitability_AsAdmin_ReturnsFile(
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

        var response = await client.GetAsync($"/api/reports/profitability/export?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("reetrack-profitability_", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(magic, bytes.Take(magic.Length).ToArray());
    }

    private sealed class ProfitabilityResponse
    {
        public IReadOnlyList<CurrencyResponse> ByCurrency { get; init; } = [];
        public IReadOnlyList<ProjectResponse> Projects { get; init; } = [];
    }

    private sealed class CurrencyResponse
    {
        public string CurrencyCode { get; init; } = "";
        public decimal Revenue { get; init; }
    }

    private sealed class ProjectResponse
    {
        public string Name { get; init; } = "";
        public string BillingModel { get; init; } = "";
        public decimal Revenue { get; init; }
    }
}
