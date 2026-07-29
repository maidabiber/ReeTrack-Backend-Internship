using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

public class ProjectService : IProjectService
{
    private const int NameMaxLength = 200;
    private const decimal EstimateMax = 100_000_000m; // fits numeric(10,2)

    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrencyService _currencyService;

    public ProjectService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICurrencyService currencyService)
    {
        _db = db;
        _currentUser = currentUser;
        _currencyService = currencyService;
    }

    public async Task<PagedResult<ProjectDto>> ListAsync(
        ProjectListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.Projects.AsNoTracking();

        switch (query.Status?.Trim().ToLowerInvariant())
        {
            case null or "" or "active":
                filtered = filtered.Where(p => p.Status == ProjectStatus.Active);
                break;
            case "archived":
                filtered = filtered.Where(p => p.Status == ProjectStatus.Archived);
                break;
            case "all":
                break;
            default:
                throw new AppException("Status must be one of: active, archived, all.");
        }

        var clientFilterIds = new List<Guid>();
        if (query.ClientId.HasValue)
            clientFilterIds.Add(query.ClientId.Value);
        if (query.ClientIds is { Count: > 0 })
            clientFilterIds.AddRange(query.ClientIds);
        if (clientFilterIds.Count > 0)
            filtered = filtered.Where(p => clientFilterIds.Contains(p.ClientId));

        var q = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
        {
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(q) ||
                p.Client.Name.ToLower().Contains(q));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            .OrderBy(p => p.Client.Name)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectRow(
                p.Id,
                p.Name,
                p.ClientId,
                p.Client.Name,
                p.Status,
                p.CreatedByUserId,
                p.CurrencyCode,
                p.HourlyRate,
                p.FixedFeeAmount,
                p.TimeEstimateHours,
                p.Color,
                p.Tasks.Count,
                p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var actualByProject = await GetActualHoursByProjectAsync(
            rows.Select(r => r.Id).ToList(),
            cancellationToken);

        return new PagedResult<ProjectDto>
        {
            Items = rows
                .Select(r => MapRow(r, actualByProject.GetValueOrDefault(r.Id)))
                .ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Projects.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProjectRow(
                p.Id,
                p.Name,
                p.ClientId,
                p.Client.Name,
                p.Status,
                p.CreatedByUserId,
                p.CurrencyCode,
                p.HourlyRate,
                p.FixedFeeAmount,
                p.TimeEstimateHours,
                p.Color,
                p.Tasks.Count,
                p.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("Project was not found.", 404);

        var actualHours = await GetActualHoursAsync(id, cancellationToken);
        return MapRow(row, actualHours);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(input.Name);
        await EnsureNameIsAvailableAsync(name, excludeId: null, cancellationToken);

        var clientName = await EnsureClientExistsAsync(input.ClientId, cancellationToken);

        var project = new Project
        {
            ClientId = input.ClientId!.Value,
            Name = name,
            Status = ProjectStatus.Active,
            // Hand-set like DeletedByUserId — the audit interceptor only owns Id/timestamps.
            CreatedByUserId = _currentUser.UserId
        };

        await ApplyBillingBlockAsync(
            project,
            input.CurrencyCode,
            input.HourlyRate,
            input.FixedFeeAmount,
            input.TimeEstimateHours,
            input.Color,
            cancellationToken);

        _db.Projects.Add(project);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return MapEntity(project, clientName, taskCount: 0, actualHours: 0m);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new AppException("Project was not found.", 404);

        if (input.Name is not null)
        {
            var normalized = NormalizeName(input.Name);
            if (!string.Equals(project.Name, normalized, StringComparison.Ordinal))
            {
                await EnsureNameIsAvailableAsync(normalized, excludeId: id, cancellationToken);
                project.Name = normalized;
            }
        }

        if (input.ClientId.HasValue)
        {
            await EnsureClientExistsAsync(input.ClientId, cancellationToken);
            project.ClientId = input.ClientId.Value;
        }

        if (input.Status is not null)
            project.Status = ParseStatus(input.Status);

        // The billing block is applied wholesale only when CurrencyCode is sent
        // (the edit form always includes it), so a status-only (archive) patch
        // leaves rate/fee/estimate untouched.
        if (input.CurrencyCode is not null)
        {
            await ApplyBillingBlockAsync(
                project,
                input.CurrencyCode,
                input.HourlyRate,
                input.FixedFeeAmount,
                input.TimeEstimateHours,
                input.Color,
                cancellationToken);
        }

        await SaveGuardingNameConflictAsync(cancellationToken);

        var clientName = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == project.ClientId)
            .Select(c => c.Name)
            .FirstAsync(cancellationToken);
        var taskCount = await _db.ProjectTasks.CountAsync(t => t.ProjectId == id, cancellationToken);
        var actualHours = await GetActualHoursAsync(id, cancellationToken);

        return MapEntity(project, clientName, taskCount, actualHours);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new AppException("Project was not found.", 404);

        var isAdmin = _currentUser.Roles.Contains("Admin");
        if (!isAdmin && project.CreatedByUserId != _currentUser.UserId)
            throw new AppException("Only the person who created this project or an admin can delete it.", 403);

        var taskIds = await _db.ProjectTasks
            .Where(t => t.ProjectId == id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var hasTrackedTime = await _db.TimeEntries.AnyAsync(
            e => e.ProjectId == id || (e.ProjectTaskId != null && taskIds.Contains(e.ProjectTaskId.Value)),
            cancellationToken);
        if (hasTrackedTime)
            throw new AppException("This project has tracked time. Archive it instead.", 409);

        var now = DateTime.UtcNow;
        var deletedBy = _currentUser.UserId;

        project.DeletedAtUtc = now;
        project.DeletedByUserId = deletedBy;

        var tasks = await _db.ProjectTasks.Where(t => t.ProjectId == id).ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.DeletedAtUtc = now;
            task.DeletedByUserId = deletedBy;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyBillingBlockAsync(
        Project project,
        string? currencyCode,
        decimal? hourlyRate,
        decimal? fixedFeeAmount,
        decimal? timeEstimateHours,
        string? color,
        CancellationToken cancellationToken)
    {

        project.CurrencyCode = await _currencyService.EnsureSupportedAsync(currencyCode, cancellationToken);
        project.HourlyRate = ValidateAmount(hourlyRate, "Hourly rate");
        project.FixedFeeAmount = ValidateAmount(fixedFeeAmount, "Fixed fee");
        project.TimeEstimateHours = ValidateEstimate(timeEstimateHours);
        project.Color = NormalizeColor(color);
    }

    private async Task<string> EnsureClientExistsAsync(Guid? clientId, CancellationToken cancellationToken)
    {
        if (!clientId.HasValue)
            throw new AppException("Client is required.");

        var name = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == clientId.Value)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (name is null)
            throw new AppException("Client was not found.");

        return name;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new AppException("Project name is required.");
        if (trimmed.Length > NameMaxLength)
            throw new AppException($"Project name must be at most {NameMaxLength} characters.");

        return trimmed;
    }

    private static string? NormalizeColor(string? color)
    {
        var trimmed = color?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (!ColorPattern.IsMatch(trimmed))
            throw new AppException("Color must be a hex value like #4366E2.");

        return trimmed.ToUpperInvariant();
    }

    private static decimal? ValidateAmount(decimal? amount, string label)
    {
        if (amount is null)
            return null;
        if (amount < 0)
            throw new AppException($"{label} cannot be negative.");

        return amount;
    }

    private static decimal? ValidateEstimate(decimal? hours)
    {
        if (hours is null)
            return null;
        if (hours < 0)
            throw new AppException("Time estimate cannot be negative.");
        if (hours >= EstimateMax)
            throw new AppException("Time estimate is too large.");

        return hours;
    }

    private static ProjectStatus ParseStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => ProjectStatus.Active,
            "archived" => ProjectStatus.Archived,
            _ => throw new AppException("Status must be one of: active, archived.")
        };

    private static string FormatStatus(ProjectStatus status) =>
        status == ProjectStatus.Archived ? "archived" : "active";

    private async Task EnsureNameIsAvailableAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var taken = await _db.Projects.AnyAsync(
            p => p.Name.ToLower() == lowered && (excludeId == null || p.Id != excludeId),
            cancellationToken);

        if (taken)
            throw new AppException("A project with this name already exists.", 409);
    }

    // Backstop for the pre-check race: ix_projects_name is unique over non-deleted rows.
    // Only a genuine unique-index violation is a name conflict; any other database
    // error must surface as a real failure rather than a misleading 409.
    private async Task SaveGuardingNameConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new AppException("A project with this name already exists.", 409);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// Confirmed time on the project itself or on any of its tasks (same attribution
    /// rule as the delete-with-tracked-time guard).
    /// </summary>
    private async Task<decimal> GetActualHoursAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var byProject = await GetActualHoursByProjectAsync([projectId], cancellationToken);
        return byProject.GetValueOrDefault(projectId);
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> GetActualHoursByProjectAsync(
        IReadOnlyList<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId))
            .Select(t => new { t.Id, t.ProjectId })
            .ToListAsync(cancellationToken);

        var taskToProject = tasks.ToDictionary(t => t.Id, t => t.ProjectId);
        var taskIds = taskToProject.Keys.ToList();

        var entries = await _db.TimeEntries.AsNoTracking()
            .Where(e => e.Status == TimeEntryStatus.Confirmed)
            .Where(e =>
                (e.ProjectId != null && projectIds.Contains(e.ProjectId.Value)) ||
                (e.ProjectTaskId != null && taskIds.Contains(e.ProjectTaskId.Value)))
            .Select(e => new { e.ProjectId, e.ProjectTaskId, e.DurationSeconds })
            .ToListAsync(cancellationToken);

        var secondsByProject = projectIds.ToDictionary(id => id, _ => 0L);
        foreach (var entry in entries)
        {
            Guid? attributedProjectId = entry.ProjectId;
            if (attributedProjectId is null &&
                entry.ProjectTaskId is Guid taskId &&
                taskToProject.TryGetValue(taskId, out var fromTask))
            {
                attributedProjectId = fromTask;
            }

            if (attributedProjectId is Guid pid && secondsByProject.ContainsKey(pid))
                secondsByProject[pid] += entry.DurationSeconds;
        }

        return secondsByProject.ToDictionary(
            pair => pair.Key,
            pair => SecondsToHours(pair.Value));
    }

    private static decimal SecondsToHours(long totalSeconds) =>
        Math.Round(totalSeconds / 3600m, 2, MidpointRounding.AwayFromZero);

    private static ProjectDto MapEntity(Project project, string clientName, int taskCount, decimal actualHours) =>
        new()
        {
            Id = project.Id,
            Name = project.Name,
            ClientId = project.ClientId,
            ClientName = clientName,
            Status = FormatStatus(project.Status),
            CreatedByUserId = project.CreatedByUserId,
            CurrencyCode = project.CurrencyCode,
            HourlyRate = project.HourlyRate,
            FixedFeeAmount = project.FixedFeeAmount,
            TimeEstimateHours = project.TimeEstimateHours,
            ActualHours = actualHours,
            Color = project.Color,
            TaskCount = taskCount,
            CreatedAtUtc = project.CreatedAtUtc
        };

    private static ProjectDto MapRow(ProjectRow row, decimal actualHours) =>
        new()
        {
            Id = row.Id,
            Name = row.Name,
            ClientId = row.ClientId,
            ClientName = row.ClientName,
            Status = FormatStatus(row.Status),
            CreatedByUserId = row.CreatedByUserId,
            CurrencyCode = row.CurrencyCode,
            HourlyRate = row.HourlyRate,
            FixedFeeAmount = row.FixedFeeAmount,
            TimeEstimateHours = row.TimeEstimateHours,
            ActualHours = actualHours,
            Color = row.Color,
            TaskCount = row.TaskCount,
            CreatedAtUtc = row.CreatedAtUtc
        };

    private sealed record ProjectRow(
        Guid Id,
        string Name,
        Guid ClientId,
        string ClientName,
        ProjectStatus Status,
        Guid CreatedByUserId,
        string CurrencyCode,
        decimal? HourlyRate,
        decimal? FixedFeeAmount,
        decimal? TimeEstimateHours,
        string? Color,
        int TaskCount,
        DateTime CreatedAtUtc);
}
