using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.TimeEntries;

public class TimeEntryTemplateService : ITimeEntryTemplateService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TimeEntryTemplateService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<TimeEntryTemplateDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.TimeEntryTemplates.AsNoTracking()
            .Where(t => t.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(t => t.Project)
            .Include(t => t.ProjectTask)
            .Include(t => t.TimeEntryTemplateTags)
                .ThenInclude(tt => tt.Tag)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TimeEntryTemplateDto>
        {
            Items = items.Select(MapTemplate).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TimeEntryTemplateDto> CreateFromTimeEntryAsync(
        Guid timeEntryId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var entry = await _db.TimeEntries.AsNoTracking()
                .Include(e => e.TimeEntryTags)
                .FirstOrDefaultAsync(e => e.Id == timeEntryId && e.UserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry");

        var alreadyExists = await _db.TimeEntryTemplates.AsNoTracking()
            .AnyAsync(t => t.TimeEntryId == timeEntryId, cancellationToken);
        if (alreadyExists)
            throw AppErrors.Conflict("A favourite template for this time entry already exists.");

        TimeEntryTemplate template;
        try
        {
            template = TimeEntryTemplate.FromTimeEntry(userId, entry);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(ex.Message, 400, ErrorCode.Validation);
        }

        foreach (var tag in entry.TimeEntryTags)
        {
            template.TimeEntryTemplateTags.Add(new TimeEntryTemplateTag
            {
                TagId = tag.TagId
            });
        }

        var now = DateTime.UtcNow;
        template.CreatedAtUtc = now;
        template.UpdatedAtUtc = now;

        _db.TimeEntryTemplates.Add(template);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw AppErrors.Conflict("A favourite template for this time entry already exists.");
        }

        var created = await _db.TimeEntryTemplates.AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.ProjectTask)
                .Include(t => t.TimeEntryTemplateTags)
                    .ThenInclude(tt => tt.Tag)
                .FirstAsync(t => t.Id == template.Id, cancellationToken);

        return MapTemplate(created);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var template = await _db.TimeEntryTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Time entry template");

        _db.TimeEntryTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static TimeEntryTemplateDto MapTemplate(TimeEntryTemplate template) =>
        new()
        {
            Id = template.Id,
            TimeEntryId = template.TimeEntryId,
            ProjectId = template.ProjectId,
            ProjectTaskId = template.ProjectTaskId,
            Description = template.Description,
            IsBillable = template.IsBillable,
            StartTimeUtc = template.StartTimeUtc,
            EndTimeUtc = template.EndTimeUtc,
            DurationSeconds = template.DurationSeconds,
            CreatedAtUtc = template.CreatedAtUtc,
            ProjectName = template.Project?.Name,
            ProjectColor = template.Project?.Color,
            ProjectTaskName = template.ProjectTask?.Name,
            Tags = template.TimeEntryTemplateTags
                .Where(t => t.Tag is not null)
                .OrderBy(t => t.Tag.Name)
                .Select(t => new TimeEntryTagDto
                {
                    Id = t.TagId,
                    Name = t.Tag.Name,
                    Color = t.Tag.Color
                })
                .ToList()
        };
}
