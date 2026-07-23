using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectCostService : IProjectCostService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostCalculator _calculator;

    public ProjectCostService(IApplicationDbContext db, IProjectCostCalculator calculator)
    {
        _db = db;
        _calculator = calculator;
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

        if (entries.Count > 0)
        {
            var entryDates = entries.Select(ResolveEntryDate).ToList();
            var firstWeekStart = GetWeekStart(entryDates.Min());
            var lastWeekEnd = GetWeekStart(entryDates.Max()).AddDays(6);
            var rangeStart = firstWeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var rangeEndExclusive = lastWeekEnd
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

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
                        holiday.Date >= firstWeekStart &&
                        holiday.Date <= lastWeekEnd)
                    .Select(holiday => holiday.Date)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var multiplierConfig = await LoadMultiplierConfigAsync(cancellationToken);

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

    private static DateOnly GetWeekStart(DateOnly date) =>
        date.AddDays(-((7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7));

    private async Task<RateMultiplierConfig> LoadMultiplierConfigAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.RateMultiplierSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
            return RateMultiplierConfig.Defaults;

        return new RateMultiplierConfig(
            settings.WeekendPremium,
            settings.HolidayPremium,
            settings.OvertimePremium,
            settings.WeeklyOvertimeThresholdHours);
    }
}
