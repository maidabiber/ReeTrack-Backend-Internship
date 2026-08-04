using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports.Writers;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal sealed class CustomReportContext
{
    private readonly ReportEntryPipeline? _pipeline;
    private readonly IProjectCostCalculator? _calculator;
    private readonly IApplicationDbContext? _db;
    private readonly ReportEntryData? _data;
    private readonly bool _needsCost;
    private readonly bool _needsProjects;
    private readonly bool _needsHourTargets;

    private IReadOnlyList<TimeEntry>? _overtimeContext;
    private IReadOnlyList<EntryCostLine>? _costLines;
    private IReadOnlyList<EntryRow>? _rows;
    private IReadOnlyList<ProjectSummaryDto>? _projectSummaries;
    private IReadOnlyDictionary<Guid, decimal>? _weeklyHourTargets;
    private bool _overtimeLoaded;

    public CustomReportContext(
        ReportEntryPipeline pipeline,
        IProjectCostCalculator calculator,
        IApplicationDbContext db,
        ReportEntryData data,
        bool needsCost,
        bool needsProjects,
        bool needsHourTargets)
    {
        _pipeline = pipeline;
        _calculator = calculator;
        _db = db;
        _data = data;
        _needsCost = needsCost;
        _needsProjects = needsProjects;
        _needsHourTargets = needsHourTargets;
        _overtimeContext = data.OvertimeContext;
        _overtimeLoaded = data.OvertimeContextLoaded;
    }

    /// <summary>Unit-test stub with pre-built rows (no DB / pipeline).</summary>
    internal static CustomReportContext ForTests(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<ProjectSummaryDto>? projectSummaries = null,
        IReadOnlyDictionary<Guid, decimal>? weeklyHourTargets = null) =>
        new(rows, projectSummaries, weeklyHourTargets);

    private CustomReportContext(
        IReadOnlyList<EntryRow> rows,
        IReadOnlyList<ProjectSummaryDto>? projectSummaries,
        IReadOnlyDictionary<Guid, decimal>? weeklyHourTargets)
    {
        _rows = rows;
        _projectSummaries = projectSummaries;
        _weeklyHourTargets = weeklyHourTargets;
        _overtimeLoaded = true;
    }

    public ReportEntryData Data =>
        _data ?? throw new InvalidOperationException("Stub context has no pipeline data.");

    public long GrandTotalSeconds =>
        _rows?.Sum(r => r.DurationSeconds)
        ?? _data!.Entries.Sum(e => (long)e.DurationSeconds);

    public IReadOnlyList<ProjectSummaryDto> ProjectSummaries =>
        _projectSummaries ?? throw new InvalidOperationException("Project summaries were not loaded.");

    public IReadOnlyDictionary<Guid, decimal> WeeklyHourTargets =>
        _weeklyHourTargets ?? throw new InvalidOperationException("Hour targets were not loaded.");

    /// <summary>True when overtime context was materialised (eagerly or lazily).</summary>
    public bool OvertimeWasLoaded => _overtimeLoaded;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_data is null)
            return;

        if (_needsCost || _needsProjects)
            await EnsureCostAsync(cancellationToken);

        _rows ??= BuildRows(_data.Entries, _costLines);

        if (_needsProjects)
            EnsureProjects();

        if (_needsHourTargets)
            await EnsureHourTargetsAsync(cancellationToken);
    }

    public IReadOnlyList<EntryRow> Rows =>
        _rows ?? throw new InvalidOperationException("Call EnsureReadyAsync before reading rows.");

    private async Task EnsureCostAsync(CancellationToken cancellationToken)
    {
        if (_costLines is not null)
            return;

        if (!_overtimeLoaded)
        {
            _overtimeContext = await _pipeline!.LoadOvertimeContextAsync(_data!.Entries, cancellationToken);
            _overtimeLoaded = true;
        }

        var overtime = _overtimeContext ?? [];
        var rates = _data!.UserRates;
        if (rates.Count == 0 && _data.Entries.Count > 0)
        {
            var userIds = (overtime.Count > 0 ? overtime.Select(e => e.UserId) : _data.Entries.Select(e => e.UserId))
                .Distinct()
                .ToList();
            rates = await _db!.UserHourlyRates
                .AsNoTracking()
                .Where(rate => userIds.Contains(rate.UserId))
                .ToListAsync(cancellationToken);
        }

        _costLines = _calculator!.CalculateEntries(
            _data.Entries,
            overtime,
            rates,
            _data.Holidays,
            _data.MultiplierConfig);
    }

    private void EnsureProjects()
    {
        if (_projectSummaries is not null)
            return;

        var overtime = _overtimeContext ?? [];
        var rates = _data!.UserRates;
        _projectSummaries = ProjectSummaryBuilder.Build(
            _calculator!,
            _data.Entries,
            overtime,
            rates.ToLookup(r => r.UserId),
            _data.Holidays,
            _data.MultiplierConfig);
    }

    private async Task EnsureHourTargetsAsync(CancellationToken cancellationToken)
    {
        if (_weeklyHourTargets is not null)
            return;

        var userIds = _data!.Entries.Select(e => e.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            _weeklyHourTargets = new Dictionary<Guid, decimal>();
            return;
        }

        var overrides = await _db!.UserHourTargets
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId))
            .ToListAsync(cancellationToken);

        var defaults = await _db.HourTargetSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var defaultWeekly = ToWeeklyHours(defaults?.Mode ?? HourTargetMode.Weekly, defaults?.TargetHours ?? 40m);
        var overrideMap = overrides.ToDictionary(t => t.UserId, t => ToWeeklyHours(t.Mode, t.TargetHours));

        _weeklyHourTargets = userIds.ToDictionary(
            id => id,
            id => overrideMap.TryGetValue(id, out var weekly) ? weekly : defaultWeekly);
    }

    private static decimal ToWeeklyHours(HourTargetMode mode, decimal targetHours) =>
        mode == HourTargetMode.Daily
            ? targetHours * 5m
            : targetHours;

    private static IReadOnlyList<EntryRow> BuildRows(
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<EntryCostLine>? costLines)
    {
        var costById = costLines?.ToDictionary(c => c.EntryId)
            ?? new Dictionary<Guid, EntryCostLine>();

        return entries.Select(entry =>
        {
            var user = entry.User;
            var userName = string.IsNullOrWhiteSpace(user?.DisplayName)
                ? user?.Email ?? entry.UserId.ToString()
                : user.DisplayName;
            var date = ReportMetadataResolver.ResolveEntryDate(entry);
            var clientId = ReportMetadataResolver.ResolveClientId(entry);
            var clientName = ReportMetadataResolver.ResolveClientName(entry);
            var tags = entry.TimeEntryTags
                .Where(t => t.Tag is not null && t.Tag.DeletedAtUtc is null)
                .Select(t => (t.TagId, t.Tag.Name))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new EntryRow(
                entry.Id,
                entry.UserId,
                userName,
                entry.ProjectId,
                entry.Project?.Name ?? ReportFormat.UnassignedLabel,
                clientId,
                string.IsNullOrWhiteSpace(clientName) ? "(No client)" : clientName,
                entry.ProjectTaskId,
                entry.ProjectTask?.Name ?? "(No task)",
                tags,
                entry.IsBillable,
                date,
                TimesheetWeek.ToWeekStart(date),
                string.IsNullOrWhiteSpace(entry.Project?.CurrencyCode)
                    ? SummaryReportAnalytics.NoCurrencyCode
                    : entry.Project!.CurrencyCode.Trim().ToUpperInvariant(),
                entry.DurationSeconds,
                entry.Description,
                costById.GetValueOrDefault(entry.Id));
        }).ToList();
    }
}
