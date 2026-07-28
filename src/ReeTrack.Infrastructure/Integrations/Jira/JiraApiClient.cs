using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Integrations.Jira;

namespace ReeTrack.Infrastructure.Integrations.Jira;

public sealed class JiraApiClient : IJiraApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public JiraApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<JiraApiProject>> ListProjectsAsync(
        string siteUrl,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeSiteUrl(siteUrl);
        var results = new List<JiraApiProject>();
        var startAt = 0;
        const int maxResults = 50;

        while (true)
        {
            var url = $"{baseUrl}/rest/api/3/project/search?startAt={startAt}&maxResults={maxResults}";
            using var request = CreateRequest(HttpMethod.Get, url, email, apiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<JiraProjectSearchResponse>(stream, JsonOptions, cancellationToken)
                ?? new JiraProjectSearchResponse();

            foreach (var project in payload.Values ?? [])
            {
                if (string.IsNullOrWhiteSpace(project.Id) || string.IsNullOrWhiteSpace(project.Key))
                    continue;

                results.Add(new JiraApiProject(project.Id, project.Key, project.Name ?? project.Key));
            }

            startAt += payload.Values?.Count ?? 0;
            if (payload.IsLast == true || payload.Values is null || payload.Values.Count == 0)
                break;
        }

        return results
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<JiraApiIssue>> ListIssuesAsync(
        string siteUrl,
        string email,
        string apiToken,
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeSiteUrl(siteUrl);
        var results = new List<JiraApiIssue>();
        string? nextPageToken = null;

        while (true)
        {
            var url = $"{baseUrl}/rest/api/3/search/jql";
            var body = new Dictionary<string, object?>
            {
                ["jql"] = $"project = \"{EscapeJql(projectKey)}\" AND issuetype NOT IN subTaskIssueTypes() ORDER BY key ASC",
                ["maxResults"] = 100,
                ["fields"] = new[] { "summary", "status", "assignee", "timetracking", "issuetype" }
            };
            if (!string.IsNullOrEmpty(nextPageToken))
                body["nextPageToken"] = nextPageToken;

            using var request = CreateRequest(HttpMethod.Post, url, email, apiToken);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return await ListIssuesLegacyAsync(baseUrl, email, apiToken, projectKey, cancellationToken);

            await EnsureSuccessAsync(response, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<JiraIssueSearchResponse>(stream, JsonOptions, cancellationToken)
                ?? new JiraIssueSearchResponse();

            AppendIssues(results, payload.Issues);

            if (string.IsNullOrEmpty(payload.NextPageToken) || payload.Issues is null || payload.Issues.Count == 0)
                break;

            nextPageToken = payload.NextPageToken;
        }

        return results;
    }

    private async Task<IReadOnlyList<JiraApiIssue>> ListIssuesLegacyAsync(
        string baseUrl,
        string email,
        string apiToken,
        string projectKey,
        CancellationToken cancellationToken)
    {
        var results = new List<JiraApiIssue>();
        var startAt = 0;
        const int maxResults = 100;

        while (true)
        {
            var url = $"{baseUrl}/rest/api/3/search?startAt={startAt}&maxResults={maxResults}" +
                      $"&jql={Uri.EscapeDataString($"project = \"{EscapeJql(projectKey)}\" AND issuetype NOT IN subTaskIssueTypes() ORDER BY key ASC")}" +
                      "&fields=summary,status,assignee,timetracking,issuetype";

            using var request = CreateRequest(HttpMethod.Get, url, email, apiToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<JiraIssueSearchResponse>(stream, JsonOptions, cancellationToken)
                ?? new JiraIssueSearchResponse();

            AppendIssues(results, payload.Issues);

            var fetched = payload.Issues?.Count ?? 0;
            startAt += fetched;
            if (fetched == 0 || startAt >= (payload.Total ?? startAt))
                break;
        }

        return results;
    }

    private static void AppendIssues(List<JiraApiIssue> results, List<JiraIssueDto>? issues)
    {
        if (issues is null) return;

        foreach (var issue in issues)
        {
            if (string.IsNullOrWhiteSpace(issue.Id) || string.IsNullOrWhiteSpace(issue.Key))
                continue;

            if (issue.Fields?.Issuetype?.Subtask == true)
                continue;

            var summary = issue.Fields?.Summary?.Trim();
            if (string.IsNullOrWhiteSpace(summary))
                summary = issue.Key;

            var category = issue.Fields?.Status?.StatusCategory?.Key;
            var isDone = string.Equals(category, "done", StringComparison.OrdinalIgnoreCase);

            decimal? estimateHours = null;
            var original = issue.Fields?.Timetracking?.OriginalEstimateSeconds
                ?? ParseEstimateSeconds(issue.Fields?.Timetracking?.OriginalEstimate);
            if (original is > 0)
                estimateHours = Math.Round(original.Value / 3600m, 2);

            results.Add(new JiraApiIssue(
                issue.Id,
                issue.Key,
                summary,
                isDone,
                issue.Fields?.Assignee?.EmailAddress,
                estimateHours));
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string email, string apiToken)
    {
        var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AppException(
            $"Jira API error ({(int)response.StatusCode}): {Truncate(body)}",
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? 401 : 502);
    }

    internal static string NormalizeSiteUrl(string siteUrl)
    {
        if (string.IsNullOrWhiteSpace(siteUrl))
            throw new AppException("Jira site URL is required.");

        var trimmed = siteUrl.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AppException("Jira site URL is invalid.");
        }

        return $"{uri.Scheme}://{uri.Authority}";
    }

    private static string EscapeJql(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";

    private static decimal? ParseEstimateSeconds(string? display)
    {
        // Display estimates like "2h" are not parsed here; prefer originalEstimateSeconds from API.
        return null;
    }

    private sealed class JiraProjectSearchResponse
    {
        public bool? IsLast { get; set; }
        public List<JiraProjectDto>? Values { get; set; }
    }

    private sealed class JiraProjectDto
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public string? Name { get; set; }
    }

    private sealed class JiraIssueSearchResponse
    {
        public string? NextPageToken { get; set; }
        public int? Total { get; set; }
        public List<JiraIssueDto>? Issues { get; set; }
    }

    private sealed class JiraIssueDto
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public JiraIssueFieldsDto? Fields { get; set; }
    }

    private sealed class JiraIssueFieldsDto
    {
        public string? Summary { get; set; }
        public JiraStatusDto? Status { get; set; }
        public JiraAssigneeDto? Assignee { get; set; }
        public JiraTimetrackingDto? Timetracking { get; set; }
        public JiraIssueTypeDto? Issuetype { get; set; }
    }

    private sealed class JiraIssueTypeDto
    {
        public bool Subtask { get; set; }
    }

    private sealed class JiraStatusDto
    {
        public JiraStatusCategoryDto? StatusCategory { get; set; }
    }

    private sealed class JiraStatusCategoryDto
    {
        public string? Key { get; set; }
    }

    private sealed class JiraAssigneeDto
    {
        public string? EmailAddress { get; set; }
    }

    private sealed class JiraTimetrackingDto
    {
        public string? OriginalEstimate { get; set; }
        public decimal? OriginalEstimateSeconds { get; set; }
    }
}
