using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectCostService : IProjectCostService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostCalculator _calculator;
    private readonly IRateMultiplierConfigProvider _multipliers;

    public ProjectCostService(
        IApplicationDbContext db,
        IProjectCostCalculator calculator,
        IRateMultiplierConfigProvider multipliers)
    {
        _db = db;
        _calculator = calculator;
        _multipliers = multipliers;
    }

    public async Task<ProjectCostDto?> GetLatestAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var projectExists = await _db.Projects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId, cancellationToken);

        if (!projectExists)
            throw new AppException("Project not found.", 404);

        var snapshot = await _db.ProjectCostSnapshots
            .AsNoTracking()
            .Include(s => s.TaskCosts)
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CalculatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
            return null;

        return ToDto(snapshot);
    }

    public async Task<ProjectCostDto> CalculateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new AppException("Project not found.", 404);

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e =>
                e.ProjectId == projectId &&
                e.Status == TimeEntryStatus.Confirmed &&
                e.DeletedAtUtc == null)
            .ToListAsync(cancellationToken);

        var userIds = entries
            .Select(e => e.UserId)
            .Distinct()
            .ToList();

        var userRates = userIds.Count == 0
            ? []
            : await _db.UserHourlyRates
                .AsNoTracking()
                .Where(r => userIds.Contains(r.UserId))
                .ToListAsync(cancellationToken);

        var crossProjectUserEntries = Array.Empty<TimeEntry>();
        var holidays = new HashSet<DateOnly>();

        if (WeekWindow.Covering(entries.Select(ResolveEntryDate)) is { } window)
        {
            var rangeStart = window.StartUtc;
            var rangeEndExclusive = window.EndExclusiveUtc;

            crossProjectUserEntries = await _db.TimeEntries
                .AsNoTracking()
                .Where(e =>
                    userIds.Contains(e.UserId) &&
                    e.Status == TimeEntryStatus.Confirmed &&
                    e.DeletedAtUtc == null &&
                    (e.StartedAtUtc ?? e.CreatedAtUtc) >= rangeStart &&
                    (e.StartedAtUtc ?? e.CreatedAtUtc) < rangeEndExclusive)
                .ToArrayAsync(cancellationToken);

            holidays = (await _db.Holidays
                    .AsNoTracking()
                    .Where(holiday =>
                        holiday.IsActive &&
                        holiday.Date >= window.FirstWeekStart &&
                        holiday.Date <= window.LastWeekEnd)
                    .Select(holiday => holiday.Date)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var multiplierConfig = await _multipliers.GetAsync(cancellationToken);

        var result = _calculator.Calculate(
            project,
            entries,
            crossProjectUserEntries,
            userRates,
            holidays,
            multiplierConfig);
        var calculatedAtUtc = DateTime.UtcNow;

        var snapshot = new ProjectCostSnapshot
        {
            ProjectId = projectId,
            CalculatedCost = result.CalculatedCost,
            TotalHours = result.TotalHours,
            WeekendHours = result.WeekendHours,
            HolidayHours = result.HolidayHours,
            OvertimeHours = result.OvertimeHours,
            CalculatedAtUtc = calculatedAtUtc,
            CreatedAtUtc = calculatedAtUtc,
            UpdatedAtUtc = calculatedAtUtc,
            TaskCosts = result.TaskCosts
                .Select(task => new ProjectTaskCostSnapshot
                {
                    ProjectTaskId = task.ProjectTaskId,
                    CalculatedCost = task.CalculatedCost,
                    TotalHours = task.TotalHours,
                    WeekendHours = task.WeekendHours,
                    HolidayHours = task.HolidayHours,
                    OvertimeHours = task.OvertimeHours,
                    CreatedAtUtc = calculatedAtUtc,
                    UpdatedAtUtc = calculatedAtUtc
                })
                .ToList()
        };

        _db.ProjectCostSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(snapshot);
    }

    private static ProjectCostDto ToDto(ProjectCostSnapshot snapshot) =>
        new()
        {
            ProjectId = snapshot.ProjectId,
            CalculatedCost = snapshot.CalculatedCost,
            TotalHours = snapshot.TotalHours,
            WeekendHours = snapshot.WeekendHours,
            HolidayHours = snapshot.HolidayHours,
            OvertimeHours = snapshot.OvertimeHours,
            CalculatedAtUtc = snapshot.CalculatedAtUtc,
            TaskCosts = snapshot.TaskCosts
                .OrderBy(task => task.ProjectTaskId)
                .Select(task => new ProjectTaskCostDto
                {
                    ProjectTaskId = task.ProjectTaskId,
                    CalculatedCost = task.CalculatedCost,
                    TotalHours = task.TotalHours,
                    WeekendHours = task.WeekendHours,
                    HolidayHours = task.HolidayHours,
                    OvertimeHours = task.OvertimeHours
                })
                .ToList()
        };

    private static DateOnly ResolveEntryDate(TimeEntry entry) =>
        DateOnly.FromDateTime(entry.StartedAtUtc ?? entry.CreatedAtUtc);
}
