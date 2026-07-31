using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryAssociationService : ITimeEntryAssociationService
{
    private readonly IApplicationDbContext _db;

    public TimeEntryAssociationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public Task ApplyForCreateAsync(
        TimeEntry entry,
        TimeEntryFieldsInput input,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            entry,
            input.ProjectId,
            input.ProjectTaskId,
            input.TagIds,
            replaceTags: true,
            cancellationToken);

    public Task ApplyForUpdateAsync(
        TimeEntry entry,
        TimeEntryFieldsInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.ProjectId is null && input.ProjectTaskId is null && input.TagIds is null)
            return Task.CompletedTask;

        return ApplyAsync(
            entry,
            input.ProjectId,
            input.ProjectTaskId,
            input.TagIds,
            replaceTags: input.TagIds is not null,
            cancellationToken);
    }

    public void CopyAssociations(TimeEntry source, TimeEntry target)
    {
        target.ClientId = source.ClientId;
        target.ProjectId = source.ProjectId;
        target.ProjectTaskId = source.ProjectTaskId;

        foreach (var tag in source.TimeEntryTags)
        {
            target.TimeEntryTags.Add(new TimeEntryTag
            {
                TagId = tag.TagId
            });
        }
    }

    private async Task ApplyAsync(
        TimeEntry entry,
        Guid? projectId,
        Guid? projectTaskId,
        IReadOnlyList<Guid>? tagIds,
        bool replaceTags,
        CancellationToken cancellationToken)
    {
        Guid? resolvedProjectId = projectId;
        Guid? resolvedTaskId = projectTaskId;
        Project? project = null;
        ProjectTask? task = null;

        if (resolvedTaskId is Guid taskId)
        {
            task = await _db.ProjectTasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
                ?? throw AppErrors.NotFound("Task");

            if (resolvedProjectId is Guid explicitProjectId && task.ProjectId != explicitProjectId)
                throw AppErrors.Validation("Task does not belong to the selected project.");

            resolvedProjectId = task.ProjectId;
            project = task.Project;
        }
        else if (resolvedProjectId is Guid projectOnlyId)
        {
            project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == projectOnlyId, cancellationToken)
                ?? throw AppErrors.NotFound("Project");
        }

        entry.ProjectId = resolvedProjectId;
        entry.ProjectTaskId = resolvedTaskId;
        entry.ClientId = project?.ClientId;
        entry.Project = project;
        entry.ProjectTask = task;

        if (!replaceTags && tagIds is null)
            return;

        var ids = (tagIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            entry.TimeEntryTags.Clear();
            return;
        }

        var tags = await _db.Tags
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (tags.Count != ids.Count)
            throw new AppException("One or more tags were not found.", 404, ErrorCode.NotFound);

        var byId = tags.ToDictionary(t => t.Id);
        entry.TimeEntryTags.Clear();
        foreach (var id in ids)
        {
            entry.TimeEntryTags.Add(new TimeEntryTag
            {
                TimeEntryId = entry.Id,
                TagId = id,
                Tag = byId[id]
            });
        }
    }
}
