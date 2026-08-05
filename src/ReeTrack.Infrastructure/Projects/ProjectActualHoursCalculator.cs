using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

/// <summary>
/// Shared rollup of confirmed time-entry hours for one or more projects.
/// Matches the rules used by project list/detail ActualHours.
/// </summary>
public static class ProjectActualHoursCalculator
{
    public static async Task<IReadOnlyDictionary<Guid, decimal>> GetActualHoursByProjectAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var tasks = await db.ProjectTasks.AsNoTracking()
            .Where(t => projectIds.Contains(t.ProjectId))
            .Select(t => new { t.Id, t.ProjectId })
            .ToListAsync(cancellationToken);

        var taskToProject = tasks.ToDictionary(t => t.Id, t => t.ProjectId);
        var taskIds = taskToProject.Keys.ToList();

        var entries = await db.TimeEntries.AsNoTracking()
            .Where(e => e.Status == TimeEntryStatus.Confirmed)
            .Where(e =>
                (e.ProjectId != null && projectIds.Contains(e.ProjectId.Value)) ||
                (e.ProjectTaskId != null && taskIds.Contains(e.ProjectTaskId.Value)))
            .Select(e => new { e.ProjectId, e.ProjectTaskId, e.DurationSeconds })
            .ToListAsync(cancellationToken);

        var secondsByProject = projectIds.ToDictionary(id => id, _ => 0L);
        foreach (var entry in entries)
        {
            Guid? projectId = entry.ProjectId;
            if (projectId is null &&
                entry.ProjectTaskId is Guid taskId &&
                taskToProject.TryGetValue(taskId, out var fromTask))
            {
                projectId = fromTask;
            }

            if (projectId is Guid pid && secondsByProject.ContainsKey(pid))
                secondsByProject[pid] += entry.DurationSeconds;
        }

        return secondsByProject.ToDictionary(
            pair => pair.Key,
            pair => SecondsToHours(pair.Value));
    }

    public static decimal SecondsToHours(long totalSeconds) =>
        Math.Round(totalSeconds / 3600m, 2, MidpointRounding.AwayFromZero);
}
