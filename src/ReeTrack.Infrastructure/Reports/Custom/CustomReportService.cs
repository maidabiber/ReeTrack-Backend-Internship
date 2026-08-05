using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Writers;

namespace ReeTrack.Infrastructure.Reports.Custom;

public sealed class CustomReportService : ICustomReportService
{
    private readonly ReportEntryPipeline _pipeline;
    private readonly IProjectCostCalculator _calculator;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter<CustomReportDto>> _writers;
    private readonly CustomReportRunCache _runCache;

    public CustomReportService(
        ReportEntryPipeline pipeline,
        IProjectCostCalculator calculator,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEnumerable<IReportWriter<CustomReportDto>> writers,
        CustomReportRunCache runCache)
    {
        _pipeline = pipeline;
        _calculator = calculator;
        _db = db;
        _currentUser = currentUser;
        _writers = writers.ToDictionary(w => w.Format);
        _runCache = runCache;
    }

    public CustomReportCatalogueDto GetCatalogue() =>
        new()
        {
            Dimensions = DimensionCatalog.All.Values
                .OrderBy(d => d.Label, StringComparer.OrdinalIgnoreCase)
                .Select(d => new DimensionCatalogueItemDto
                {
                    Id = d.Id,
                    Label = d.Label,
                    FansOut = d.FansOut
                })
                .ToList(),
            Metrics = MetricCatalog.All.Values
                .OrderBy(m => m.Label, StringComparer.OrdinalIgnoreCase)
                .Select(m => new MetricCatalogueItemDto
                {
                    Id = m.Id,
                    Label = m.Label,
                    Unit = m.Unit,
                    Scope = m.Scope,
                    CompatibleDimensions = MetricCompatibility.CompatibleDimensions(m)
                })
                .ToList(),
            BlockTypes =
            [
                new() { Type = "kpi", Label = "KPI row" },
                new() { Type = "breakdown", Label = "Breakdown table" },
                new() { Type = "chart", Label = "Chart" },
                new() { Type = "entries", Label = "Entries" },
                new() { Type = "note", Label = "Note" },
                new() { Type = "narrative", Label = "Narrative summary" },
            ],
            EntryColumns = EntryColumnCatalog.All
                .OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new EntryColumnCatalogueItemDto { Id = kv.Key, Label = kv.Value })
                .ToList(),
            Operators = Enum.GetNames<ComputedOperator>()
        };

    public async Task<CustomReportDto> RunAsync(
        CustomReportSpec spec,
        CancellationToken cancellationToken = default)
    {
        CustomReportSpecValidator.Validate(spec);
        var (needsCost, needsProjects, needsHourTargets) = CustomReportSpecValidator.AnalyzeNeeds(spec);

        // Skip overtime context until a cost / project metric asks for it.
        var data = await _pipeline.LoadAsync(
            spec.Query,
            loadOvertimeContext: false,
            cancellationToken);

        var context = new CustomReportContext(
            _pipeline,
            _calculator,
            _db,
            data,
            needsCost,
            needsProjects,
            needsHourTargets);

        await context.EnsureReadyAsync(cancellationToken);

        IReadOnlyList<ReportBlockResult> blocks = spec.Blocks
            .Select(block => BlockEvaluators.Evaluate(block, context))
            .ToList();

        // Apply PctOfTotal computed columns now that we have full-table totals.
        blocks = ApplyPctOfTotal(spec, blocks);

        var basics = ComputeBasicKpis(context.Rows);
        var schedule = ComputeScheduleHours(context.Rows, costLoaded: needsCost || needsProjects);
        var generatedAt = DateTime.UtcNow;

        var report = new CustomReportDto
        {
            Kpis = new ReportKpisDto
            {
                TotalSeconds = basics.TotalSeconds,
                BillableSeconds = basics.BillableSeconds,
                NonBillableSeconds = basics.TotalSeconds - basics.BillableSeconds,
                BillablePct = SummaryReportAnalytics.BillablePct(basics.BillableSeconds, basics.TotalSeconds),
                EntryCount = context.Rows.Count,
                ActiveMembers = context.Rows.Select(r => r.UserId).Distinct().Count(),
                ActiveProjects = context.Rows.Where(r => r.ProjectId is not null).Select(r => r.ProjectId!.Value).Distinct().Count(),
                OvertimeHours = schedule.OvertimeHours,
                WeekendHours = schedule.WeekendHours,
                HolidayHours = schedule.HolidayHours,
                UnassignedSeconds = basics.UnassignedSeconds
            },
            Basis = new ReportBasisDto
            {
                WeekendPremium = data.MultiplierConfig.WeekendPremium,
                HolidayPremium = data.MultiplierConfig.HolidayPremium,
                OvertimePremium = data.MultiplierConfig.OvertimePremium,
                WeeklyOvertimeThresholdHours = data.MultiplierConfig.WeeklyOvertimeThresholdHours
            },
            GeneratedAtUtc = generatedAt,
            GeneratedByName = await ReportMetadataResolver.ResolveGeneratedByAsync(_db, _currentUser, cancellationToken),
            FirstEntryDate = context.Rows.Count == 0 ? null : context.Rows.Min(r => r.Date),
            FilterFromDate = data.Query.From,
            FilterToDate = data.Query.To,
            Blocks = blocks,
            Warnings = []
        };

        _runCache.Set(_currentUser.UserId, CustomReportFingerprint.ComputeCacheKey(spec), report);
        return report;
    }

    public async Task<CustomReportDto> GetOrRunAsync(
        CustomReportSpec spec,
        CancellationToken cancellationToken = default)
    {
        if (_runCache.TryGet(_currentUser.UserId, CustomReportFingerprint.ComputeCacheKey(spec), out var cached))
            return cached;

        return await RunAsync(spec, cancellationToken);
    }

    public async Task<ReportFile> ExportAsync(
        CustomReportSpec spec,
        ReportExportFormat format,
        CancellationToken cancellationToken = default)
    {
        if (!_writers.TryGetValue(format, out var writer))
            throw new AppException($"Unsupported export format '{format}'.", 400, ErrorCode.ExportFormatInvalid);

        var model = await GetOrRunAsync(spec, cancellationToken);
        return writer.Write(model);
    }

    private static IReadOnlyList<ReportBlockResult> ApplyPctOfTotal(
        CustomReportSpec spec,
        IReadOnlyList<ReportBlockResult> blocks)
    {
        var result = blocks.ToList();
        for (var i = 0; i < spec.Blocks.Count; i++)
        {
            if (spec.Blocks[i] is not BreakdownBlockSpec breakdown || result[i] is not TableResult table)
                continue;

            var pctColumns = breakdown.Computed
                .Where(c => c.Operator == ComputedOperator.PctOfTotal)
                .ToList();
            if (pctColumns.Count == 0)
                continue;

            var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in pctColumns)
            {
                var sum = table.Rows.Sum(r => r.Cells.GetValueOrDefault(column.Left)?.Number ?? 0m);
                totals[column.Id] = sum;
            }

            TableCell PctCell(decimal? left, decimal total)
            {
                decimal? value = total <= 0m || left is null
                    ? null
                    : Math.Round(left.Value * 100m / total, 2, MidpointRounding.AwayFromZero);
                return new TableCell
                {
                    Number = value,
                    Display = value is null ? "—" : ReportFormat.Percent(value.Value)
                };
            }

            var updatedRows = table.Rows.Select(row =>
            {
                var cells = new Dictionary<string, TableCell>(row.Cells, StringComparer.OrdinalIgnoreCase);
                foreach (var column in pctColumns)
                {
                    var left = cells.GetValueOrDefault(column.Left)?.Number;
                    cells[column.Id] = PctCell(left, totals[column.Id]);
                }

                return new TableRow { Key = row.Key, Cells = cells };
            }).ToList();

            TableRow? updatedTotals = table.Totals;
            if (updatedTotals is not null)
            {
                var cells = new Dictionary<string, TableCell>(updatedTotals.Cells, StringComparer.OrdinalIgnoreCase);
                foreach (var column in pctColumns)
                {
                    // Totals row for % of total is always 100% when there is a positive base.
                    cells[column.Id] = PctCell(
                        totals[column.Id] <= 0m ? null : totals[column.Id],
                        totals[column.Id]);
                }

                updatedTotals = new TableRow { Key = updatedTotals.Key, Cells = cells };
            }

            result[i] = new TableResult
            {
                Id = table.Id,
                Title = table.Title,
                Footnote = table.Footnote,
                Columns = table.Columns,
                Rows = updatedRows,
                Totals = updatedTotals
            };
        }

        return result;
    }

    private sealed record BasicKpis(long TotalSeconds, long BillableSeconds, long UnassignedSeconds);

    private sealed record ScheduleHours(decimal OvertimeHours, decimal WeekendHours, decimal HolidayHours);

    private static BasicKpis ComputeBasicKpis(IReadOnlyList<EntryRow> rows)
    {
        var totalSeconds = rows.Sum(r => r.DurationSeconds);
        var billableSeconds = rows.Where(r => r.IsBillable).Sum(r => r.DurationSeconds);
        var unassignedSeconds = rows.Where(r => r.ProjectId is null).Sum(r => r.DurationSeconds);
        return new BasicKpis(totalSeconds, billableSeconds, unassignedSeconds);
    }

    /// <summary>
    /// When cost lines were loaded, prefer calculator buckets. Otherwise derive weekend
    /// from the calendar so hours-only reports still show a real weekend figure; OT /
    /// holiday stay 0 without inventing allocation rules.
    /// </summary>
    private static ScheduleHours ComputeScheduleHours(IReadOnlyList<EntryRow> rows, bool costLoaded)
    {
        if (costLoaded)
        {
            return new ScheduleHours(
                ReportRounding.Hours(rows.Sum(r => r.Cost?.OvertimeHours ?? 0m)),
                ReportRounding.Hours(rows.Sum(r => r.Cost?.WeekendHours ?? 0m)),
                ReportRounding.Hours(rows.Sum(r => r.Cost?.HolidayHours ?? 0m)));
        }

        var weekendSeconds = rows
            .Where(r => r.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .Sum(r => r.DurationSeconds);

        return new ScheduleHours(
            OvertimeHours: 0m,
            WeekendHours: SummaryReportAnalytics.Hours(weekendSeconds),
            HolidayHours: 0m);
    }
}
