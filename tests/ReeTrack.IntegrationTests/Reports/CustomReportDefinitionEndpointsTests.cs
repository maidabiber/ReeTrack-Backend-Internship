using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class CustomReportDefinitionEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ListDefinitions_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/custom/definitions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDefinitions_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/custom/definitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Definitions_CrudFlow_PersistsSpec()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (admin, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var createResponse = await client.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Utilization overview", "Weekly KPIs"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Utilization overview", created.Name);
        Assert.Equal("Weekly KPIs", created.Description);
        Assert.Equal(admin.Id, created.CreatedByUserId);
        Assert.Equal(CustomReportVisibility.Shared, created.Visibility);
        Assert.True(created.CanEdit);
        Assert.Equal(1, created.Spec.Blocks.Count);

        var list = await client.GetFromJsonAsync<PagedResponse>(
            "/api/reports/custom/definitions?page=1&pageSize=10",
            JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        var listed = Assert.Single(list.Items);
        Assert.Equal(created.Id, listed.Id);
        Assert.True(listed.CanEdit);

        var fetched = await client.GetFromJsonAsync<DefinitionResponse>(
            $"/api/reports/custom/definitions/{created.Id}",
            JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.Name, fetched.Name);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/reports/custom/definitions/{created.Id}",
            SaveRequest("Utilization v2", "Updated KPIs"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Utilization v2", updated.Name);
        Assert.Equal("Updated KPIs", updated.Description);

        var deleteResponse = await client.DeleteAsync($"/api/reports/custom/definitions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var empty = await client.GetFromJsonAsync<PagedResponse>(
            "/api/reports/custom/definitions",
            JsonOptions);
        Assert.NotNull(empty);
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task CreateDefinition_DifferentUsers_CanShareTheSameName()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (_, otherToken) = await SeedAdditionalAdminAsync(factory, "namesake-a@reetrack.test", "Namesake A");
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var first = await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions", SaveRequest("Q3 Margin"), JsonOptions);
        var second = await otherClient.PostAsJsonAsync(
            "/api/reports/custom/definitions", SaveRequest("Q3 Margin"), JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task ListDefinitions_HidesAnotherUsersPrivateReport()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, ownerToken) = await factory.SeedAdminAsync();
        var (_, otherToken) = await SeedAdditionalAdminAsync(factory, "viewer@reetrack.test", "Viewer Admin");
        var ownerClient = factory.CreateAuthenticatedClient(ownerToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var created = await (await ownerClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Private notes", visibility: CustomReportVisibility.Private),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);
        Assert.NotNull(created);

        var list = await otherClient.GetFromJsonAsync<PagedResponse>(
            "/api/reports/custom/definitions", JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(0, list.TotalCount);

        var getResponse = await otherClient.GetAsync($"/api/reports/custom/definitions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DuplicateDefinition_AssignsCallerAndSuffixesName()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var (otherAdmin, otherToken) = await SeedAdditionalAdminAsync(
            factory,
            "other-admin@reetrack.test",
            "Other Admin");
        var adminClient = factory.CreateAuthenticatedClient(adminToken);
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var created = await (await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Margin report"),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);

        Assert.NotNull(created);

        var duplicateResponse = await otherClient.PostAsync(
            $"/api/reports/custom/definitions/{created.Id}/duplicate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);
        Assert.NotNull(duplicate);
        Assert.Equal("Margin report (copy)", duplicate.Name);
        Assert.Equal(otherAdmin.Id, duplicate.CreatedByUserId);
        Assert.Equal(created.Description, duplicate.Description);
        Assert.Equal(created.Spec.Version, duplicate.Spec.Version);

        var list = await otherClient.GetFromJsonAsync<PagedResponse>(
            "/api/reports/custom/definitions",
            JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task CreateDefinition_DuplicateName_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var first = await client.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Audit view"),
            JsonOptions);
        var duplicate = await client.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest(" audit VIEW "),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task UpdateDefinition_AsDifferentAdmin_ReturnsForbidden()
    {
        // Ownership, not role, gates writes now — a Shared report is viewable by every admin
        // but editable only by whoever created it.
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Shared report"),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);

        Assert.NotNull(created);

        var (_, otherToken) = await SeedAdditionalAdminAsync(
            factory,
            "editor@reetrack.test",
            "Editor Admin");
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var updateResponse = await otherClient.PutAsJsonAsync(
            $"/api/reports/custom/definitions/{created.Id}",
            SaveRequest("Updated by another admin"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteDefinition_AsDifferentAdmin_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Disposable report"),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);

        Assert.NotNull(created);

        var (_, otherToken) = await SeedAdditionalAdminAsync(
            factory,
            "deleter@reetrack.test",
            "Deleter Admin");
        var otherClient = factory.CreateAuthenticatedClient(otherToken);

        var deleteResponse = await otherClient.DeleteAsync(
            $"/api/reports/custom/definitions/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        var list = await adminClient.GetFromJsonAsync<PagedResponse>(
            "/api/reports/custom/definitions",
            JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
    }

    [Fact]
    public async Task UpdateDefinition_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Admin report"),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);

        Assert.NotNull(created);

        var (_, memberToken) = await factory.SeedMemberAsync("member-editor@reetrack.test");
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var response = await memberClient.PutAsJsonAsync(
            $"/api/reports/custom/definitions/{created.Id}",
            SaveRequest("Should fail"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDefinition_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, adminToken) = await factory.SeedAdminAsync();
        var adminClient = factory.CreateAuthenticatedClient(adminToken);

        var created = await (await adminClient.PostAsJsonAsync(
            "/api/reports/custom/definitions",
            SaveRequest("Protected report"),
            JsonOptions)).Content.ReadFromJsonAsync<DefinitionResponse>(JsonOptions);

        Assert.NotNull(created);

        var (_, memberToken) = await factory.SeedMemberAsync("member-deleter@reetrack.test");
        var memberClient = factory.CreateAuthenticatedClient(memberToken);

        var response = await memberClient.DeleteAsync(
            $"/api/reports/custom/definitions/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync(
            $"/api/reports/custom/definitions/{created.Id}")).StatusCode);
    }

    private static object SaveRequest(
        string name,
        string? description = null,
        CustomReportVisibility visibility = CustomReportVisibility.Shared) =>
        new
        {
            name,
            description,
            visibility = visibility.ToString(),
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
                        metrics = new[] { "totalHours", "entryCount" }
                    }
                }
            }
        };

    private static async Task<(User Admin, string AccessToken)> SeedAdditionalAdminAsync(
        ReeTrackWebApplicationFactory factory,
        string email,
        string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var now = DateTime.UtcNow;
        var admin = new User
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

    private sealed class PagedResponse
    {
        public IReadOnlyList<DefinitionResponse> Items { get; init; } = [];
        public int TotalCount { get; init; }
    }

    private sealed class DefinitionResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public SpecResponse Spec { get; init; } = new();
        public int SchemaVersion { get; init; }
        public Guid CreatedByUserId { get; init; }
        public CustomReportVisibility Visibility { get; init; }
        public bool CanEdit { get; init; }
    }

    private sealed class SpecResponse
    {
        public int Version { get; init; }
        public IReadOnlyList<object> Blocks { get; init; } = [];
    }
}
