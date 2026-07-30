using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Writers;
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

        var basics = ComputeBasicKpis(entries);

        var activity = ReportAggregations.BuildActivity(
            entries.Select(e => (ReportMetadataResolver.ResolveEntryDate(e).DayOfWeek, (long)e.DurationSeconds)));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var trendEndDate = data.Query.To is { } to && to < today ? to : today;
        var trendEndWeek = TimesheetWeek.ToWeekStart(trendEndDate);
        var weeklyTrend = ReportAggregations.BuildWeeklyTrend(
            entries.Select(e => (ReportMetadataResolver.ResolveEntryInstant(e), (long)e.DurationSeconds)),
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

        var projects = ProjectSummaryBuilder.Build(
            _calculator,
            entries,
            data.OvertimeContext,
            ratesByUser,
            data.Holidays,
            data.MultiplierConfig);

        return new SummaryReportDto
        {
            Kpis = BuildKpis(
                basics,
                entries.Count,
                activeMembers: members.Count,
                activeProjects: projects.Count,
                overtimeHours: projects.Sum(p => p.OvertimeHours),
                weekendHours: projects.Sum(p => p.WeekendHours),
                holidayHours: projects.Sum(p => p.HolidayHours)),
            Basis = MapBasis(data.MultiplierConfig),
            Activity = activity,
            WeeklyTrend = weeklyTrend,
            Projects = projects,
            Members = members,
            GeneratedAtUtc = DateTime.UtcNow,
            FirstEntryDate = entries.Count == 0
                ? null
                : entries.Min(ReportMetadataResolver.ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
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
            .Select(entry => DetailedEntryMapper.Map(entry, costByEntryId.GetValueOrDefault(entry.Id)))
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

        var basics = ComputeBasicKpis(entries);

        return new DetailedReportDto
        {
            Kpis = BuildKpis(
                basics,
                entries.Count,
                activeMembers: entries.Select(e => e.UserId).Distinct().Count(),
                activeProjects: entries
                    .Where(e => e.ProjectId is not null && e.Project is not null)
                    .Select(e => e.ProjectId!.Value)
                    .Distinct()
                    .Count(),
                overtimeHours: ReportRounding.Hours(costLines.Sum(line => line.OvertimeHours)),
                weekendHours: ReportRounding.Hours(costLines.Sum(line => line.WeekendHours)),
                holidayHours: ReportRounding.Hours(costLines.Sum(line => line.HolidayHours))),
            Basis = MapBasis(data.MultiplierConfig),
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
            FirstEntryDate = entries.Count == 0 ? null : entries.Min(ReportMetadataResolver.ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            Entries = pageEntries,
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            Groups = groups
        };
    }

    public async Task<WorkloadReportDto> GetWorkloadAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _pipeline.LoadAsync(query, cancellationToken);
        var entries = data.Entries;
        var (allocations, grandTotal, grandBillable) = WorkloadMatrixBuilder.Build(entries);

        var costLines = _calculator.CalculateEntries(
            entries,
            data.OvertimeContext,
            data.UserRates,
            data.Holidays,
            data.MultiplierConfig);

        var basics = ComputeBasicKpis(entries);

        var overtimeHours = ReportRounding.Hours(costLines.Sum(line => line.OvertimeHours));
        var weekendHours = ReportRounding.Hours(costLines.Sum(line => line.WeekendHours));
        var holidayHours = ReportRounding.Hours(costLines.Sum(line => line.HolidayHours));

        return new WorkloadReportDto
        {
            Kpis = BuildKpis(
                basics,
                entries.Count,
                activeMembers: allocations.Select(a => a.UserId).Distinct().Count(),
                activeProjects: entries
                    .Where(e => e.ProjectId is not null && e.Project is not null)
                    .Select(e => e.ProjectId!.Value)
                    .Distinct()
                    .Count(),
                overtimeHours,
                weekendHours,
                holidayHours),
            Basis = MapBasis(data.MultiplierConfig),
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
            FirstEntryDate = entries.Count == 0 ? null : entries.Min(ReportMetadataResolver.ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            Allocations = allocations,
            GrandTotalSeconds = grandTotal,
            GrandTotalBillableSeconds = grandBillable,
            Schedule =
            [
                new WorkloadScheduleDto
                {
                    Label = "Overtime",
                    Hours = overtimeHours,
                    PctOfTotalHours = SummaryReportAnalytics.PctOfTotal(
                        (long)Math.Round(overtimeHours * 3600m, MidpointRounding.AwayFromZero),
                        basics.TotalSeconds)
                },
                new WorkloadScheduleDto
                {
                    Label = "Weekend",
                    Hours = weekendHours,
                    PctOfTotalHours = SummaryReportAnalytics.PctOfTotal(
                        (long)Math.Round(weekendHours * 3600m, MidpointRounding.AwayFromZero),
                        basics.TotalSeconds)
                },
                new WorkloadScheduleDto
                {
                    Label = "Holiday",
                    Hours = holidayHours,
                    PctOfTotalHours = SummaryReportAnalytics.PctOfTotal(
                        (long)Math.Round(holidayHours * 3600m, MidpointRounding.AwayFromZero),
                        basics.TotalSeconds)
                }
            ]
        };
    }

    public async Task<ProfitabilityReportDto> GetProfitabilityAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _pipeline.LoadAsync(query, cancellationToken);
        var entries = data.Entries;
        var ratesByUser = data.UserRates.ToLookup(rate => rate.UserId);

        var projects = ProjectSummaryBuilder.Build(
            _calculator,
            entries,
            data.OvertimeContext,
            ratesByUser,
            data.Holidays,
            data.MultiplierConfig);

        var billableByProject = entries
            .Where(e => e.ProjectId is not null)
            .GroupBy(e => e.ProjectId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds));

        var (projectRows, byCurrency) = ProfitabilityRollupBuilder.Build(projects, billableByProject);

        var costLines = _calculator.CalculateEntries(
            entries,
            data.OvertimeContext,
            data.UserRates,
            data.Holidays,
            data.MultiplierConfig);
        var costByEntryId = costLines.ToDictionary(line => line.EntryId);

        var members = entries
            .GroupBy(e => (
                e.UserId,
                Currency: NormaliseCurrency(e.Project?.CurrencyCode)))
            .Select(g =>
            {
                var user = g.First().User;
                var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
                    ? user?.Email ?? g.Key.UserId.ToString()
                    : user.DisplayName;
                return new MemberLabourCostDto
                {
                    UserId = g.Key.UserId,
                    DisplayName = displayName,
                    CurrencyCode = g.Key.Currency,
                    TotalSeconds = g.Sum(e => (long)e.DurationSeconds),
                    LabourCost = ReportRounding.Cost(
                        g.Sum(e => costByEntryId.GetValueOrDefault(e.Id)?.CalculatedCost ?? 0m))
                };
            })
            .OrderByDescending(m => m.LabourCost)
            .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var trendEndDate = data.Query.To is { } to && to < today ? to : today;
        var trendEndWeek = TimesheetWeek.ToWeekStart(trendEndDate);
        var weeklyTrend = ProfitabilityTrendBuilder.Build(entries, projectRows, trendEndWeek);

        var basics = ComputeBasicKpis(entries);

        return new ProfitabilityReportDto
        {
            Kpis = BuildKpis(
                basics,
                entries.Count,
                activeMembers: entries.Select(e => e.UserId).Distinct().Count(),
                activeProjects: projectRows.Count,
                overtimeHours: ReportRounding.Hours(costLines.Sum(line => line.OvertimeHours)),
                weekendHours: ReportRounding.Hours(costLines.Sum(line => line.WeekendHours)),
                holidayHours: ReportRounding.Hours(costLines.Sum(line => line.HolidayHours))),
            Basis = MapBasis(data.MultiplierConfig),
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByName = await ResolveGeneratedByAsync(cancellationToken),
            FirstEntryDate = entries.Count == 0 ? null : entries.Min(ReportMetadataResolver.ResolveEntryDate),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            ByCurrency = byCurrency,
            WeeklyTrend = weeklyTrend,
            Projects = projectRows,
            Members = members,
            RevenueBasisLines = ReportFormat.ProfitabilityRevenueLines()
        };
    }

    /// <summary>
    /// The six KPI fields every report shares (total/billable/non-billable seconds,
    /// billable %, entry count, unassigned seconds) plus the per-report figures that
    /// vary (active members/projects, overtime/weekend/holiday hours).
    /// </summary>
    private static ReportKpisDto BuildKpis(
        BasicKpis basics,
        int entryCount,
        int activeMembers,
        int activeProjects,
        decimal overtimeHours,
        decimal weekendHours,
        decimal holidayHours) =>
        new()
        {
            TotalSeconds = basics.TotalSeconds,
            BillableSeconds = basics.BillableSeconds,
            NonBillableSeconds = basics.TotalSeconds - basics.BillableSeconds,
            BillablePct = ReportAggregations.BillablePct(basics.BillableSeconds, basics.TotalSeconds),
            EntryCount = entryCount,
            ActiveMembers = activeMembers,
            ActiveProjects = activeProjects,
            OvertimeHours = overtimeHours,
            WeekendHours = weekendHours,
            HolidayHours = holidayHours,
            UnassignedSeconds = basics.UnassignedSeconds
        };

    private static string NormaliseCurrency(string? currencyCode) =>
        string.IsNullOrWhiteSpace(currencyCode)
            ? SummaryReportAnalytics.NoCurrencyCode
            : currencyCode.Trim().ToUpperInvariant();

    private sealed record BasicKpis(long TotalSeconds, long BillableSeconds, long UnassignedSeconds);

    private static BasicKpis ComputeBasicKpis(IReadOnlyList<TimeEntry> entries)
    {
        var totalSeconds = entries.Sum(e => (long)e.DurationSeconds);
        var billableSeconds = entries.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds);
        var unassignedSeconds = entries
            .Where(e => e.ProjectId is null || e.Project is null)
            .Sum(e => (long)e.DurationSeconds);
        return new BasicKpis(totalSeconds, billableSeconds, unassignedSeconds);
    }

    private static ReportBasisDto MapBasis(RateMultiplierConfig config) =>
        new()
        {
            WeekendPremium = config.WeekendPremium,
            HolidayPremium = config.HolidayPremium,
            OvertimePremium = config.OvertimePremium,
            WeeklyOvertimeThresholdHours = config.WeeklyOvertimeThresholdHours
        };

    private Task<string?> ResolveGeneratedByAsync(CancellationToken cancellationToken) =>
        ReportMetadataResolver.ResolveGeneratedByAsync(_db, _currentUser, cancellationToken);
}
