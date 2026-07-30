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

    public async Task<DetailedReportDto> GetDetailedAsync(
        ReportQuery query,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _pipeline.LoadAsync(query, cancellationToken);
        var entries = data.Entries;

        var costLines = _calculator.CalculateEntries(
            entries,
            data.OvertimeContext,
            data.UserRates,
            data.Holidays,
            data.MultiplierConfig);
        var costByEntryId = costLines.ToDictionary(line => line.EntryId);

        var detailedEntries = entries
            .Select(entry => MapDetailedEntry(entry, costByEntryId.GetValueOrDefault(entry.Id)))
            .ToList();

        var sorted = DetailedReportGrouping.Sort(detailedEntries, data.Query.GroupBy);
        var groups = DetailedReportGrouping.BuildGroups(sorted, data.Query.GroupBy);

        var totalCount = sorted.Count;
        var effectivePage = page < 1 ? 1 : page;
        IReadOnlyList<DetailedEntryDto> pageEntries;
        int effectivePageSize;

        if (pageSize <= 0)
        {
            effectivePageSize = totalCount == 0 ? 1 : totalCount;
            effectivePage = 1;
            pageEntries = sorted;
        }
        else
        {
            effectivePageSize = pageSize;
            pageEntries = sorted
                .Skip((effectivePage - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .ToList();
        }

        var totalSeconds = entries.Sum(e => (long)e.DurationSeconds);
        var billableSeconds = entries.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds);
        var unassignedSeconds = entries
            .Where(e => e.ProjectId is null || e.Project is null)
            .Sum(e => (long)e.DurationSeconds);

        return new DetailedReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = totalSeconds,
                BillableSeconds = billableSeconds,
                NonBillableSeconds = totalSeconds - billableSeconds,
                BillablePct = ReportAggregations.BillablePct(billableSeconds, totalSeconds),
                EntryCount = entries.Count,
                ActiveMembers = entries.Select(e => e.UserId).Distinct().Count(),
                ActiveProjects = entries
                    .Where(e => e.ProjectId is not null && e.Project is not null)
                    .Select(e => e.ProjectId!.Value)
                    .Distinct()
                    .Count(),
                OvertimeHours = costLines.Sum(line => line.OvertimeHours),
                WeekendHours = costLines.Sum(line => line.WeekendHours),
                HolidayHours = costLines.Sum(line => line.HolidayHours),
                UnassignedSeconds = unassignedSeconds
            },
            Basis = new ReportBasisDto
            {
                WeekendPremium = data.MultiplierConfig.WeekendPremium,
                HolidayPremium = data.MultiplierConfig.HolidayPremium,
                OvertimePremium = data.MultiplierConfig.OvertimePremium,
                WeeklyOvertimeThresholdHours = data.MultiplierConfig.WeeklyOvertimeThresholdHours
            },
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
            FirstEntryDate = entries.Count == 0 ? null : entries.Min(ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            Entries = pageEntries,
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            Groups = groups
        };
    }

    private static DetailedEntryDto MapDetailedEntry(TimeEntry entry, EntryCostLine? cost)
    {
        var user = entry.User;
        var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
            ? user?.Email ?? entry.UserId.ToString()
            : user.DisplayName;

        var clientName = entry.Client?.Name
            ?? entry.Project?.Client?.Name;
        var clientId = entry.ClientId
            ?? entry.Project?.ClientId
            ?? entry.Client?.Id
            ?? entry.Project?.Client?.Id;

        var emptyCost = cost is null;
        return new DetailedEntryDto
        {
            EntryId = entry.Id,
            EntryDate = ResolveEntryDate(entry),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            UserId = entry.UserId,
            DisplayName = displayName,
            ClientId = clientId,
            ClientName = clientName,
            ProjectId = entry.ProjectId,
            ProjectName = entry.Project?.Name,
            TaskId = entry.ProjectTaskId,
            TaskName = entry.ProjectTask?.Name,
            Tags = entry.TimeEntryTags
                .Where(t => t.Tag is not null && t.Tag.DeletedAtUtc is null)
                .Select(t => t.Tag.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            DurationSeconds = entry.DurationSeconds,
            CurrencyCode = entry.Project?.CurrencyCode,
            CalculatedCost = emptyCost ? 0m : cost!.CalculatedCost,
            NormalCost = emptyCost ? 0m : cost!.NormalCost,
            WeekendCost = emptyCost ? 0m : cost!.WeekendCost,
            HolidayCost = emptyCost ? 0m : cost!.HolidayCost,
            OvertimeCost = emptyCost ? 0m : cost!.OvertimeCost,
            OvertimeHours = emptyCost ? 0m : cost!.OvertimeHours,
            WeekendHours = emptyCost ? 0m : cost!.WeekendHours,
            HolidayHours = emptyCost ? 0m : cost!.HolidayHours,
            IsWeekend = cost?.IsWeekend
                ?? ResolveEntryDate(entry).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            IsHoliday = cost?.IsHoliday ?? false
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
