using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

public class ProjectTaskService : IProjectTaskService
{
    private const int NameMaxLength = 200;
    private const decimal EstimateMax = 100_000_000m; // fits numeric(10,2)

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ProjectTaskService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ProjectTaskDto>> ListAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!query.ProjectId.HasValue)
            throw new AppException("Project is required.");

        var projectId = query.ProjectId.Value;
        await EnsureProjectExistsAsync(projectId, cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.ProjectTasks.AsNoTracking().Where(t => t.ProjectId == projectId);

        switch (query.Status?.Trim().ToLowerInvariant())
        {
            case null or "" or "all":
                break;
            case "open":
                filtered = filtered.Where(t => t.Status == ProjectTaskStatus.Open);
                break;
            case "done":
                filtered = filtered.Where(t => t.Status == ProjectTaskStatus.Done);
                break;
            default:
                throw new AppException("Status must be one of: open, done, all.");
        }

        var q = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
            filtered = filtered.Where(t => t.Name.ToLower().Contains(q));

        var totalCount = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskRow(
                t.Id,
                t.ProjectId,
                t.Name,
                t.Status,
                t.AssignedToUserId,
                t.AssignedToUser != null ? (t.AssignedToUser.DisplayName ?? t.AssignedToUser.Email) : null,
                t.TimeEstimateHours,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectTaskDto>
        {
            Items = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<ProjectTaskDto>> ListOpenAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = query.Q?.Trim().ToLowerInvariant();

        var filtered = _db.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Status == ProjectTaskStatus.Open);

        if (!string.IsNullOrEmpty(q))
        {
            filtered = filtered.Where(t =>
                t.Name.ToLower().Contains(q) ||
                t.Project.Name.ToLower().Contains(q));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            .OrderBy(t => t.Project.Name)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaskRow(
                t.Id,
                t.ProjectId,
                t.Name,
                t.Status,
                t.AssignedToUserId,
                t.AssignedToUser != null ? (t.AssignedToUser.DisplayName ?? t.AssignedToUser.Email) : null,
                t.TimeEstimateHours,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectTaskDto>
        {
            Items = rows.Select(MapRow).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProjectTaskDto> CreateAsync(
        Guid projectId,
        CreateTaskInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureProjectExistsAsync(projectId, cancellationToken);

        var name = NormalizeName(input.Name);
        await EnsureNameIsAvailableAsync(projectId, name, excludeId: null, cancellationToken);

        var assignedToName = await ResolveAssigneeAsync(input.AssignedToUserId, cancellationToken);

        var task = new ProjectTask
        {
            ProjectId = projectId,
            Name = name,
            Status = ProjectTaskStatus.Open,
            AssignedToUserId = input.AssignedToUserId,
            TimeEstimateHours = ValidateEstimate(input.TimeEstimateHours)
        };

        _db.ProjectTasks.Add(task);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return MapEntity(task, assignedToName);
    }

    public async Task<ProjectTaskDto> UpdateAsync(
        Guid projectId,
        Guid taskId,
        UpdateTaskInput input,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.ProjectTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new AppException("Task was not found.", 404);

        if (input.Status is not null)
            task.Status = ParseStatus(input.Status);

        // Name present => full content update: assignee and estimate are applied
        // as sent (null clears), matching the edit form. Status-only patch (above)
        // just toggles done/open.
        if (input.Name is not null)
        {
            var normalized = NormalizeName(input.Name);
            if (!string.Equals(task.Name, normalized, StringComparison.Ordinal))
            {
                await EnsureNameIsAvailableAsync(projectId, normalized, excludeId: taskId, cancellationToken);
                task.Name = normalized;
            }

            task.AssignedToUserId = input.AssignedToUserId;
            task.TimeEstimateHours = ValidateEstimate(input.TimeEstimateHours);
        }

        var assignedToName = await ResolveAssigneeAsync(task.AssignedToUserId, cancellationToken);

        await SaveGuardingNameConflictAsync(cancellationToken);

        return MapEntity(task, assignedToName);
    }

    public async Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.ProjectTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken)
            ?? throw new AppException("Task was not found.", 404);

        var hasTrackedTime = await _db.TimeEntries.AnyAsync(e => e.ProjectTaskId == taskId, cancellationToken);
        if (hasTrackedTime)
            throw new AppException("This task has tracked time. Mark it done instead.", 409);

        task.DeletedAtUtc = DateTime.UtcNow;
        task.DeletedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProjectExistsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var exists = await _db.Projects.AnyAsync(p => p.Id == projectId, cancellationToken);
        if (!exists)
            throw new AppException("Project was not found.", 404);
    }

    private async Task<string?> ResolveAssigneeAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
            return null;

        var name = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => u.DisplayName ?? u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (name is null)
            throw new AppException("Assigned user was not found.");

        return name;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new AppException("Task name is required.");
        if (trimmed.Length > NameMaxLength)
            throw new AppException($"Task name must be at most {NameMaxLength} characters.");

        return trimmed;
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

    private static ProjectTaskStatus ParseStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "open" => ProjectTaskStatus.Open,
            "done" => ProjectTaskStatus.Done,
            _ => throw new AppException("Status must be one of: open, done.")
        };

    private static string FormatStatus(ProjectTaskStatus status) =>
        status == ProjectTaskStatus.Done ? "done" : "open";

    private async Task EnsureNameIsAvailableAsync(
        Guid projectId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var taken = await _db.ProjectTasks.AnyAsync(
            t => t.ProjectId == projectId
                && t.Name.ToLower() == lowered
                && (excludeId == null || t.Id != excludeId),
            cancellationToken);

        if (taken)
            throw new AppException("A task with this name already exists in this project.", 409);
    }

    // Backstop for the pre-check race: ix_project_tasks_project_id_name is unique over non-deleted rows.
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
            throw new AppException("A task with this name already exists in this project.", 409);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ProjectTaskDto MapEntity(ProjectTask task, string? assignedToName) =>
        new()
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            Name = task.Name,
            Status = FormatStatus(task.Status),
            AssignedToUserId = task.AssignedToUserId,
            AssignedToName = assignedToName,
            TimeEstimateHours = task.TimeEstimateHours,
            CreatedAtUtc = task.CreatedAtUtc
        };

    private static ProjectTaskDto MapRow(TaskRow row) =>
        new()
        {
            Id = row.Id,
            ProjectId = row.ProjectId,
            Name = row.Name,
            Status = FormatStatus(row.Status),
            AssignedToUserId = row.AssignedToUserId,
            AssignedToName = row.AssignedToName,
            TimeEstimateHours = row.TimeEstimateHours,
            CreatedAtUtc = row.CreatedAtUtc
        };

    private sealed record TaskRow(
        Guid Id,
        Guid ProjectId,
        string Name,
        ProjectTaskStatus Status,
        Guid? AssignedToUserId,
        string? AssignedToName,
        decimal? TimeEstimateHours,
        DateTime CreatedAtUtc);
}
