using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostCalculator _calculator;
    private readonly ICurrentUserService _currentUser;
    private readonly ReportEntryPipeline _pipeline;

    public ReportService(
        IApplicationDbContext db,
        IProjectCostCalculator calculator,
        ICurrentUserService currentUser,
        ReportEntryPipeline pipeline)
    {
        _db = db;
        _calculator = calculator;
        _currentUser = currentUser;
        _pipeline = pipeline;
    }

    public async Task<SummaryReportDto> GetSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _pipeline.LoadAsync(query, cancellationToken);
        var entries = data.Entries;
        var ratesByUser = data.UserRates.ToLookup(rate => rate.UserId);

        var totalSeconds = entries.Sum(e => (long)e.DurationSeconds);
        var billableSeconds = entries.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds);
        var nonBillableSeconds = totalSeconds - billableSeconds;
        // Entries with no project never reach BuildProjectSummaries, so the project
        // rows alone do not add up to TotalSeconds. Surfaced so every breakdown ties.
        var unassignedSeconds = entries
            .Where(e => e.ProjectId is null || e.Project is null)
            .Sum(e => (long)e.DurationSeconds);

        var activity = ReportAggregations.BuildActivity(
            entries.Select(e => (ResolveEntryDate(e).DayOfWeek, (long)e.DurationSeconds)));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var trendEndDate = data.Query.To is { } to && to < today ? to : today;
        var trendEndWeek = TimesheetWeek.ToWeekStart(trendEndDate);
        // Same StartedAtUtc ?? CreatedAtUtc rule as activity / cost calculator.
        var weeklyTrend = ReportAggregations.BuildWeeklyTrend(
            entries.Select(e => (ResolveEntryInstant(e), (long)e.DurationSeconds)),
            trendEndWeek);

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

        var projects = BuildProjectSummaries(
            entries,
            data.OvertimeContext,
            ratesByUser,
            data.Holidays,
            data.MultiplierConfig);

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
                HolidayHours = projects.Sum(p => p.HolidayHours),
                UnassignedSeconds = unassignedSeconds
            },
            Activity = activity,
            WeeklyTrend = weeklyTrend,
            Projects = projects,
            Members = members,
            GeneratedAtUtc = DateTime.UtcNow,
            FirstEntryDate = entries.Count == 0
                ? null
                : entries.Min(ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
            Basis = new ReportBasisDto
            {
                WeekendPremium = data.MultiplierConfig.WeekendPremium,
                HolidayPremium = data.MultiplierConfig.HolidayPremium,
                OvertimePremium = data.MultiplierConfig.OvertimePremium,
                WeeklyOvertimeThresholdHours = data.MultiplierConfig.WeeklyOvertimeThresholdHours
            }
        };
    }

    private IReadOnlyList<ProjectSummaryDto> BuildProjectSummaries(
        IReadOnlyList<TimeEntry> selectedEntries,
        IReadOnlyList<TimeEntry> overtimeContext,
        ILookup<Guid, UserHourlyRate> ratesByUser,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig)
    {
        var projectGroups = selectedEntries
            .Where(e => e.ProjectId is not null && e.Project is not null)
            .GroupBy(e => e.ProjectId!.Value)
            .ToList();

        // Index once instead of rescanning the whole portfolio per project: the window
        // slice below was O(projects × entries) and allocated a fresh list each pass.
        // Order is irrelevant — ProjectCostCalculator sorts by instant itself.
        var entriesByUser = overtimeContext
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<ProjectSummaryDto>(projectGroups.Count);

        foreach (var group in projectGroups)
        {
            var project = group.First().Project!;
            var projectEntries = group.ToList();
            var userIds = projectEntries.Select(e => e.UserId).Distinct().ToHashSet();

            // Same cross-project week window as ProjectCostService, sliced from the
            // already-loaded portfolio set (no per-project queries).
            // A GroupBy group is never empty, so the window always resolves.
            var window = WeekWindow.Covering(projectEntries.Select(ResolveEntryDate))!.Value;
            var crossProjectUserEntries = userIds
                .SelectMany(id => entriesByUser[id])
                .Where(e => window.Contains(ResolveEntryInstant(e)))
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
                ClientName = project.Client?.Name ?? string.Empty,
                Status = project.Status.ToString(),
                HourlyRate = project.HourlyRate,
                FixedFeeAmount = project.FixedFeeAmount,
                TimeEstimateHours = project.TimeEstimateHours,
                TotalSeconds = projectEntries.Sum(e => (long)e.DurationSeconds),
                CalculatedCost = cost.CalculatedCost,
                NormalCost = cost.NormalCost,
                WeekendCost = cost.WeekendCost,
                HolidayCost = cost.HolidayCost,
                OvertimeCost = cost.OvertimeCost,
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

    /// <summary>
    /// Who ran the report, for export provenance. Never throws — an unresolvable user
    /// degrades the footer line, it must not fail the report.
    /// </summary>
    private async Task<string?> ResolveGeneratedByAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return null;

        var userId = _currentUser.UserId;
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
    }

    private static DateTime ResolveEntryInstant(TimeEntry entry) =>
        entry.StartedAtUtc ?? entry.CreatedAtUtc;

    private static DateOnly ResolveEntryDate(TimeEntry entry) =>
        DateOnly.FromDateTime(ResolveEntryInstant(entry));
}
