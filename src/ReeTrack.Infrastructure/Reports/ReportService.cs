using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostCalculator _calculator;

    public ReportService(IApplicationDbContext db, IProjectCostCalculator calculator)
    {
        _db = db;
        _calculator = calculator;
    }

    public async Task<SummaryReportDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters so soft-deleted projects still resolve on Include;
        // live confirmed entries on a deleted project must appear in the breakdown.
        var entries = await _db.TimeEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.Status == TimeEntryStatus.Confirmed && e.DeletedAtUtc == null)
            .Include(e => e.User)
            .Include(e => e.Project)
            .ToListAsync(cancellationToken);

        var userIds = entries.Select(e => e.UserId).Distinct().ToList();
        var userRates = userIds.Count == 0
            ? new List<UserHourlyRate>()
            : await _db.UserHourlyRates
                .AsNoTracking()
                .Where(r => userIds.Contains(r.UserId))
                .ToListAsync(cancellationToken);
        var ratesByUser = userRates.ToLookup(r => r.UserId);

        var holidays = (await _db.Holidays
                .AsNoTracking()
                .Select(h => h.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var multiplierConfig = await LoadMultiplierConfigAsync(cancellationToken);

        var totalSeconds = entries.Sum(e => (long)e.DurationSeconds);
        var billableSeconds = entries.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds);
        var nonBillableSeconds = totalSeconds - billableSeconds;

        var activity = ReportAggregations.BuildActivity(
            entries.Select(e => (ResolveEntryDate(e).DayOfWeek, (long)e.DurationSeconds)));

        var currentWeek = TimesheetWeek.ToWeekStart(DateTime.UtcNow);
        // Same StartedAtUtc ?? CreatedAtUtc rule as activity / cost calculator.
        var weeklyTrend = ReportAggregations.BuildWeeklyTrend(
            entries.Select(e => (ResolveEntryInstant(e), (long)e.DurationSeconds)),
            currentWeek);

        var members = entries
            .GroupBy(e => e.UserId)
            .Select(g =>
            {
                var user = g.First().User;
                return new MemberHoursDto
                {
                    UserId = g.Key,
                    DisplayName = string.IsNullOrWhiteSpace(user?.DisplayName)
                        ? user?.Email ?? g.Key.ToString()
                        : user.DisplayName,
                    TotalSeconds = g.Sum(e => (long)e.DurationSeconds)
                };
            })
            .OrderByDescending(m => m.TotalSeconds)
            .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var projects = BuildProjectSummaries(entries, ratesByUser, holidays, multiplierConfig);

        return new SummaryReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = totalSeconds,
                BillableSeconds = billableSeconds,
                NonBillableSeconds = nonBillableSeconds,
                BillablePct = ReportAggregations.BillablePct(billableSeconds, totalSeconds),
                EntryCount = entries.Count,
                ActiveMembers = members.Count,
                ActiveProjects = projects.Count,
                OvertimeHours = projects.Sum(p => p.OvertimeHours),
                WeekendHours = projects.Sum(p => p.WeekendHours),
                HolidayHours = projects.Sum(p => p.HolidayHours)
            },
            Activity = activity,
            WeeklyTrend = weeklyTrend,
            Projects = projects,
            Members = members,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private IReadOnlyList<ProjectSummaryDto> BuildProjectSummaries(
        IReadOnlyList<TimeEntry> allEntries,
        ILookup<Guid, UserHourlyRate> ratesByUser,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig)
    {
        var projectGroups = allEntries
            .Where(e => e.ProjectId is not null && e.Project is not null)
            .GroupBy(e => e.ProjectId!.Value)
            .ToList();

        var results = new List<ProjectSummaryDto>(projectGroups.Count);

        foreach (var group in projectGroups)
        {
            var project = group.First().Project!;
            var projectEntries = group.ToList();
            var userIds = projectEntries.Select(e => e.UserId).Distinct().ToHashSet();

            var entryDates = projectEntries.Select(ResolveEntryDate).ToList();
            var firstWeekStart = TimesheetWeek.ToWeekStart(
                entryDates.Min().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var lastWeekEnd = TimesheetWeek.ToWeekStart(
                entryDates.Max().ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddDays(6);
            var rangeStart = firstWeekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var rangeEndExclusive = lastWeekEnd
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            // Same cross-project week window as ProjectCostService, sliced from the
            // already-loaded portfolio set (one pass, no per-project queries).
            var crossProjectUserEntries = allEntries
                .Where(e =>
                {
                    if (!userIds.Contains(e.UserId))
                        return false;
                    var instant = ResolveEntryInstant(e);
                    return instant >= rangeStart && instant < rangeEndExclusive;
                })
                .ToList();

            var projectRates = userIds
                .SelectMany(id => ratesByUser[id])
                .ToList();

            var cost = _calculator.Calculate(
                project,
                projectEntries,
                crossProjectUserEntries,
                projectRates,
                holidays,
                multiplierConfig);

            results.Add(new ProjectSummaryDto
            {
                ProjectId = project.Id,
                Name = project.Name,
                CurrencyCode = project.CurrencyCode,
                TotalSeconds = projectEntries.Sum(e => (long)e.DurationSeconds),
                CalculatedCost = cost.CalculatedCost,
                OvertimeHours = cost.OvertimeHours,
                WeekendHours = cost.WeekendHours,
                HolidayHours = cost.HolidayHours
            });
        }

        return results
            .OrderByDescending(p => p.TotalSeconds)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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

    private static DateTime ResolveEntryInstant(TimeEntry entry) =>
        entry.StartedAtUtc ?? entry.CreatedAtUtc;

    private static DateOnly ResolveEntryDate(TimeEntry entry) =>
        DateOnly.FromDateTime(ResolveEntryInstant(entry));
}
