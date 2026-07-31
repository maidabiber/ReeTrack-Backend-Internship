using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Application.Integrations.Jira.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Integrations.Jira;

public sealed class JiraIntegrationService : IJiraIntegrationService
{
    private readonly IApplicationDbContext _db;
    private readonly IJiraApiClient _jiraApi;
    private readonly JiraOptions _options;

    public JiraIntegrationService(
        IApplicationDbContext db,
        IJiraApiClient jiraApi,
        IOptions<JiraOptions> options)
    {
        _db = db;
        _jiraApi = jiraApi;
        _options = options.Value;
    }

    public Task<JiraConnectionDto> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return Task.FromResult(new JiraConnectionDto(false, null, null));

        var siteUrl = JiraApiClient.NormalizeSiteUrl(_options.SiteUrl);
        return Task.FromResult(new JiraConnectionDto(true, siteUrl, _options.Email.Trim()));
    }

    public async Task<IReadOnlyList<JiraRemoteProjectDto>> ListRemoteProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        var (siteUrl, email, apiToken) = GetCredentials();
        var remote = await _jiraApi.ListProjectsAsync(siteUrl, email, apiToken, cancellationToken);

        var integrated = await _db.Projects
            .AsNoTracking()
            .Where(p => p.ExternalProvider == ExternalProvider.Jira && p.ExternalId != null)
            .Select(p => new { p.Id, p.ExternalId, p.ClientId, ClientName = p.Client.Name })
            .ToListAsync(cancellationToken);

        var byExternalId = integrated
            .Where(p => p.ExternalId is not null)
            .ToDictionary(p => p.ExternalId!, StringComparer.Ordinal);

        return remote.Select(project =>
        {
            byExternalId.TryGetValue(project.Id, out var local);
            return new JiraRemoteProjectDto(
                project.Id,
                project.Key,
                project.Name,
                local is not null,
                local?.Id,
                local?.ClientId,
                local?.ClientName);
        }).ToList();
    }

    public async Task<IntegrateJiraProjectResult> IntegrateProjectAsync(
        IntegrateJiraProjectInput input,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (input.ClientId == Guid.Empty)
            throw AppErrors.Validation("Assign a client before integrating this project.");

        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == input.ClientId && c.IsActive, cancellationToken)
            ?? throw AppErrors.NotFound("Client");

        var jiraProjectId = input.JiraProjectId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(jiraProjectId))
            throw AppErrors.Validation("Jira project id is required.");

        var already = await _db.Projects
            .AsNoTracking()
            .AnyAsync(
                p => p.ExternalProvider == ExternalProvider.Jira && p.ExternalId == jiraProjectId,
                cancellationToken);
        if (already)
            throw AppErrors.Conflict("This Jira project is already integrated.");

        var (siteUrl, email, apiToken) = GetCredentials();
        var remoteProjects = await _jiraApi.ListProjectsAsync(siteUrl, email, apiToken, cancellationToken);
        var remote = remoteProjects.FirstOrDefault(p => p.Id == jiraProjectId)
            ?? throw AppErrors.NotFound("Jira project");

        var projectName = await ResolveUniqueProjectNameAsync(remote.Name, remote.Key, cancellationToken);
        var now = DateTime.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = projectName,
            Status = ProjectStatus.Active,
            CreatedByUserId = userId,
            CurrencyCode = "EUR",
            ExternalProvider = ExternalProvider.Jira,
            ExternalId = remote.Id,
            ExternalKey = remote.Key,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        var imported = await SyncIssuesIntoProjectAsync(project, remote.Key, siteUrl, email, apiToken, cancellationToken);

        return new IntegrateJiraProjectResult(
            project.Id,
            project.Name,
            imported,
            $"Integrated {project.Name} ({imported} tasks).");
    }

    public async Task<IntegrateJiraProjectResult> SyncProjectAsync(
        Guid reeTrackProjectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == reeTrackProjectId, cancellationToken)
            ?? throw AppErrors.NotFound("Project");

        if (project.ExternalProvider != ExternalProvider.Jira
            || string.IsNullOrWhiteSpace(project.ExternalId)
            || string.IsNullOrWhiteSpace(project.ExternalKey))
        {
            throw AppErrors.Validation("This project is not linked to Jira.");
        }

        var (siteUrl, email, apiToken) = GetCredentials();

        try
        {
            var imported = await SyncIssuesIntoProjectAsync(
                project,
                project.ExternalKey,
                siteUrl,
                email,
                apiToken,
                cancellationToken);

            project.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return new IntegrateJiraProjectResult(
                project.Id,
                project.Name,
                imported,
                $"Synced {imported} tasks for {project.Name}.");
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new AppException($"Jira sync failed: {ex.Message}", 502, ErrorCode.ServiceUnavailable);
        }
    }

    private async Task<int> SyncIssuesIntoProjectAsync(
        Project project,
        string projectKey,
        string siteUrl,
        string email,
        string apiToken,
        CancellationToken cancellationToken)
    {
        var issues = await _jiraApi.ListIssuesAsync(siteUrl, email, apiToken, projectKey, cancellationToken);

        var existing = await _db.ProjectTasks
            .Where(t => t.ProjectId == project.Id && t.ExternalProvider == ExternalProvider.Jira)
            .ToListAsync(cancellationToken);

        var byExternalId = existing
            .Where(t => t.ExternalId is not null)
            .ToDictionary(t => t.ExternalId!, StringComparer.Ordinal);

        var usersByEmail = await _db.Users
            .AsNoTracking()
            .Where(u => u.Email != null)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        var emailMap = usersByEmail
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var usedNames = await _db.ProjectTasks
            .Where(t => t.ProjectId == project.Id)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        var nameSet = new HashSet<string>(usedNames, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var touched = 0;

        foreach (var issue in issues)
        {
            Guid? assigneeId = null;
            if (!string.IsNullOrWhiteSpace(issue.AssigneeEmail)
                && emailMap.TryGetValue(issue.AssigneeEmail, out var userId))
            {
                assigneeId = userId;
            }

            var desiredName = BuildTaskName(issue.Key, issue.Summary);

            if (byExternalId.TryGetValue(issue.Id, out var task))
            {
                task.Name = EnsureUniqueName(desiredName, nameSet, task.Name);
                task.Status = issue.IsDone ? ProjectTaskStatus.Done : ProjectTaskStatus.Open;
                task.AssignedToUserId = assigneeId;
                task.TimeEstimateHours = issue.OriginalEstimateHours;
                task.ExternalKey = issue.Key;
                task.UpdatedAtUtc = now;
                touched++;
                continue;
            }

            var uniqueName = EnsureUniqueName(desiredName, nameSet, null);
            var created = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = uniqueName,
                Status = issue.IsDone ? ProjectTaskStatus.Done : ProjectTaskStatus.Open,
                AssignedToUserId = assigneeId,
                TimeEstimateHours = issue.OriginalEstimateHours,
                ExternalProvider = ExternalProvider.Jira,
                ExternalId = issue.Id,
                ExternalKey = issue.Key,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.ProjectTasks.Add(created);
            byExternalId[issue.Id] = created;
            touched++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return touched;
    }

    private (string SiteUrl, string Email, string ApiToken) GetCredentials()
    {
        if (!_options.IsConfigured)
            throw new AppException(
                "Jira is not configured. Set Jira__SiteUrl, Jira__Email, and Jira__ApiToken in the environment.",
                503,
                ErrorCode.ServiceUnavailable);

        return (
            JiraApiClient.NormalizeSiteUrl(_options.SiteUrl),
            _options.Email.Trim(),
            _options.ApiToken.Trim());
    }

    private async Task<string> ResolveUniqueProjectNameAsync(
        string preferredName,
        string key,
        CancellationToken cancellationToken)
    {
        var baseName = Truncate(preferredName.Trim(), 200);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = key;

        var exists = await _db.Projects.AnyAsync(p => p.Name == baseName, cancellationToken);
        if (!exists)
            return baseName;

        var withKey = Truncate($"{baseName} ({key})", 200);
        exists = await _db.Projects.AnyAsync(p => p.Name == withKey, cancellationToken);
        if (!exists)
            return withKey;

        for (var i = 2; i < 100; i++)
        {
            var candidate = Truncate($"{baseName} ({key}-{i})", 200);
            exists = await _db.Projects.AnyAsync(p => p.Name == candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        throw AppErrors.Validation("Could not create a unique project name for this Jira project.");
    }

    private static string BuildTaskName(string key, string summary)
    {
        var combined = $"{key}: {summary.Trim()}";
        return Truncate(combined, 200);
    }

    private static string EnsureUniqueName(string desired, HashSet<string> used, string? currentName)
    {
        if (currentName is not null)
            used.Remove(currentName);

        if (!used.Contains(desired))
        {
            used.Add(desired);
            return desired;
        }

        for (var i = 2; i < 1000; i++)
        {
            var suffix = $" ({i})";
            var candidate = Truncate(desired, 200 - suffix.Length) + suffix;
            if (!used.Contains(candidate))
            {
                used.Add(candidate);
                return candidate;
            }
        }

        var fallback = Truncate($"{desired}-{Guid.NewGuid():N}", 200);
        used.Add(fallback);
        return fallback;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
