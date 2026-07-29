using System.Net;
using System.Net.Http.Json;
using ReeTrack.IntegrationTests.Support;
using Xunit;

namespace ReeTrack.IntegrationTests.Reports;

public class ReportFilterSetEndpointsTests
{
    [Fact]
    public async Task FilterSets_AsMember_ReturnsForbidden()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedMemberAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/reports/filter-sets");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FilterSets_CrudFlow_PersistsTypedQuery()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var userId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            Request("Monthly utilization", userId, "project"));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<FilterSetResponse>();
        Assert.NotNull(created);
        Assert.Equal("Monthly utilization", created.Name);
        Assert.Equal([userId], created.Query.UserIds);
        Assert.Equal(["project"], created.Query.GroupBy);
        Assert.Equal(1, created.SchemaVersion);

        var list = await client.GetFromJsonAsync<PagedResponse>(
            "/api/reports/filter-sets?page=1&pageSize=10");
        Assert.NotNull(list);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(created.Id, Assert.Single(list.Items).Id);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/reports/filter-sets/{created.Id}",
            Request("Monthly billable", userId, "client"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<FilterSetResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Monthly billable", updated.Name);
        Assert.Equal(["client"], updated.Query.GroupBy);

        var deleteResponse = await client.DeleteAsync($"/api/reports/filter-sets/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var empty = await client.GetFromJsonAsync<PagedResponse>("/api/reports/filter-sets");
        Assert.NotNull(empty);
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task CreateFilterSet_BlankName_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new { name = "  ", query = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_MissingQuery_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new { name = "Missing query" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_NameTooLong_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new { name = new string('a', 101), query = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_NullCollections_TreatsThemAsEmpty()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new
            {
                name = "Empty filters",
                query = new
                {
                    userIds = (Guid[]?)null,
                    projectIds = (Guid[]?)null,
                    groupBy = (string[]?)null
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_StartAfterEnd_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new
            {
                name = "Invalid dates",
                query = new { from = "2026-07-02", to = "2026-07-01" }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_QueryTooLarge_ReturnsBadRequest()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);
        var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();

        var response = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new
            {
                name = "Too large",
                query = new
                {
                    userIds = ids,
                    projectIds = ids,
                    clientIds = ids,
                    taskIds = ids,
                    tagIds = ids
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFilterSet_DuplicateName_ReturnsConflict()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var first = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new { name = "Audit view", query = new { } });
        var duplicate = await client.PostAsJsonAsync(
            "/api/reports/filter-sets",
            new { name = " audit VIEW ", query = new { } });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task UpdateFilterSet_UnknownId_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync(
            $"/api/reports/filter-sets/{Guid.NewGuid()}",
            new { name = "Missing", query = new { } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFilterSet_UnknownId_ReturnsNotFound()
    {
        using var factory = new ReeTrackWebApplicationFactory();
        var (_, token) = await factory.SeedAdminAsync();
        var client = factory.CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync($"/api/reports/filter-sets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object Request(string name, Guid userId, string groupBy) =>
        new
        {
            name,
            query = new
            {
                userIds = new[] { userId },
                billable = true,
                from = "2026-07-01",
                to = "2026-07-31",
                groupBy = new[] { groupBy }
            }
        };

    private sealed class PagedResponse
    {
        public IReadOnlyList<FilterSetResponse> Items { get; init; } = [];
        public int TotalCount { get; init; }
    }

    private sealed class FilterSetResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public QueryResponse Query { get; init; } = new();
        public int SchemaVersion { get; init; }
    }

    private sealed class QueryResponse
    {
        public IReadOnlyList<Guid> UserIds { get; init; } = [];
        public IReadOnlyList<string> GroupBy { get; init; } = [];
    }
}
