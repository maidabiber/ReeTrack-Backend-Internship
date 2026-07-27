using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class ReportExportEndpointsTests
{
    private static DateOnly CurrentWeek => TimesheetWeek.ToWeekStart(DateTime.UtcNow);

    [Fact]
    public async Task Export_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/summary/export?format=csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("csv", "text/csv", new byte[] { 0xEF, 0xBB, 0xBF })] // UTF-8 BOM
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { (byte)'P', (byte)'K' })]
    [InlineData("pdf", "application/pdf", new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F' })]
    public async Task Export_AsAdmin_ReturnsFile_WithExpectedTypeAndMagicBytes(
        string format,
        string contentType,
        byte[] magic)
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, adminToken) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(adminToken);

        var clientId = await SeedClientAsync(factory, "Acme");
        var projectId = await SeedProjectAsync(factory, clientId, "Alpha", hourlyRate: 50m);
        var monday = CurrentWeek.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await SeedEntryAsync(factory, admin.Id, projectId, monday.AddHours(9), 3600, isBillable: true);

        var response = await client.GetAsync($"/api/reports/summary/export?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition.DispositionType);
        Assert.StartsWith("reetrack-summary_", response.Content.Headers.ContentDisposition.FileName?.Trim('"'));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > magic.Length);
        Assert.Equal(magic, bytes.Take(magic.Length).ToArray());
    }

    [Fact]
    public async Task Export_InvalidFormat_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/summary/export?format=docx");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        decimal? hourlyRate = null)
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
