using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;

namespace ReeTrack.IntegrationTests.Support;

public class ReeTrackWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ReeTrackTests_{Guid.NewGuid()}";

    public FakeEmailSender EmailSender { get; } = new();

    public ReeTrackWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", "reetrack-test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "reetrack-api-test");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-at-least-32-chars");
        Environment.SetEnvironmentVariable("Jwt__ExpiryMinutes", "60");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DatabaseName"] = _databaseName,
                ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Jwt:Issuer"] = "reetrack-test",
                ["Jwt:Audience"] = "reetrack-api-test",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Frontend:Origin"] = "http://localhost:5173",
                ["Email:SmtpHost"] = "smtp.test.invalid",
                ["Email:From"] = "ReeTrack <no-reply@reetrack.test>",
                ["Invitation:ExpiryDays"] = "7",
                // Pin the allowed domain so tests do not inherit the developer's
                // .env (Program.cs loads it from any ancestor directory).
                ["Invitation:AllowedDomains:0"] = "reetrack.test",
                ["App:Name"] = "ReeTrack"
            });
        });

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList())
                services.Remove(descriptor);

            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    public async Task<(User Admin, string AccessToken)> SeedAdminAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        await EnsureReferenceDataAsync(db);

        var now = DateTime.UtcNow;
        var admin = new User
        {
            Email = "admin@reetrack.test",
            DisplayName = "Test Admin",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles =
            [
                new UserRole
                {
                    RoleId = RoleIds.Admin,
                    AssignedAtUtc = now
                }
            ]
        };
        admin.AssignInitialHourlyRate(DateOnly.FromDateTime(now));

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var token = jwt.CreateAccessToken(admin, ["Admin"], out _);
        return (admin, token);
    }

    public async Task<(User Member, string AccessToken)> SeedMemberAsync(
        string email = "member@reetrack.test",
        string displayName = "Test Member")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        await EnsureReferenceDataAsync(db);

        var now = DateTime.UtcNow;
        var member = new User
        {
            Email = email,
            DisplayName = displayName,
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UserRoles =
            [
                new UserRole
                {
                    RoleId = RoleIds.Member,
                    AssignedAtUtc = now
                }
            ]
        };
        member.AssignInitialHourlyRate(DateOnly.FromDateTime(now));

        db.Users.Add(member);
        await db.SaveChangesAsync();

        var token = jwt.CreateAccessToken(member, ["Member"], out _);
        return (member, token);
    }

    public HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task EnsureReferenceDataAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var seedTimestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Id = RoleIds.Admin, Name = "Admin", CreatedAtUtc = seedTimestamp, UpdatedAtUtc = seedTimestamp },
                new Role { Id = RoleIds.Member, Name = "Member", CreatedAtUtc = seedTimestamp, UpdatedAtUtc = seedTimestamp });
            await db.SaveChangesAsync();
        }

        if (!await db.Currencies.AnyAsync(c => c.Code == "EUR"))
        {
            db.Currencies.Add(new Currency
            {
                Code = "EUR",
                Name = "Euro",
                IsActive = true,
                CreatedAtUtc = seedTimestamp,
                UpdatedAtUtc = seedTimestamp
            });
            await db.SaveChangesAsync();
        }
    }
}
