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
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TimeEntryTemplateDto
            {
                Id = t.Id,
                TimeEntryId = t.TimeEntryId,
                ProjectId = t.ProjectId,
                ProjectTaskId = t.ProjectTaskId,
                Description = t.Description,
                IsBillable = t.IsBillable,
                StartTimeUtc = t.StartTimeUtc,
                EndTimeUtc = t.EndTimeUtc,
                DurationSeconds = t.DurationSeconds,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TimeEntryTemplateDto>
        {
            Items = items,
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
                .FirstOrDefaultAsync(e => e.Id == timeEntryId && e.UserId == userId, cancellationToken)
            ?? throw new AppException("Time entry was not found.", 404);

        var alreadyExists = await _db.TimeEntryTemplates.AsNoTracking()
            .AnyAsync(t => t.TimeEntryId == timeEntryId, cancellationToken);
        if (alreadyExists)
            throw new AppException("A favourite template for this time entry already exists.", 409);

        TimeEntryTemplate template;
        try
        {
            template = TimeEntryTemplate.FromTimeEntry(userId, entry);
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(ex.Message, 400);
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
            throw new AppException("A favourite template for this time entry already exists.", 409);
        }

        return MapTemplate(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var template = await _db.TimeEntryTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken)
            ?? throw new AppException("Time entry template was not found.", 404);

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
            CreatedAtUtc = template.CreatedAtUtc
        };
}
