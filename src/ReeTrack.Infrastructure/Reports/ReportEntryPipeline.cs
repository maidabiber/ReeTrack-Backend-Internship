using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportEntryPipeline
{
    private readonly IApplicationDbContext _db;
    private readonly IRateMultiplierConfigProvider _multipliers;
    private readonly ReportOptions _options;

    public ReportEntryPipeline(
        IApplicationDbContext db,
        IRateMultiplierConfigProvider multipliers,
        IOptions<ReportOptions> options)
    {
        _db = db;
        _multipliers = multipliers;
        _options = options.Value;
    }

    public Task<ReportEntryData> LoadAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default) =>
        LoadAsync(query, loadOvertimeContext: true, cancellationToken);

    /// <param name="loadOvertimeContext">
    /// When false, overtime week context is skipped. Callers that need cost / OT metrics
    /// must load it later via <see cref="LoadOvertimeContextAsync"/>.
    /// </param>
    public async Task<ReportEntryData> LoadAsync(
        ReportQuery query,
        bool loadOvertimeContext,
        CancellationToken cancellationToken = default)
    {
        var normalized = ReportQueryRules.NormalizeAndValidate(query);
        var selectedQuery = ApplyFilters(BaseEntryQuery(), normalized);

        var maxEntries = Math.Max(1, _options.MaxEntriesPerReport);
        var entryCount = await selectedQuery.CountAsync(cancellationToken);
        if (entryCount > maxEntries)
        {
            throw AppErrors.Validation(
                $"This report matches {entryCount:N0} entries, which exceeds the limit of {maxEntries:N0}. " +
                "Narrow the date range or add project / client / member filters.");
        }

        var entries = await selectedQuery
            .AsSplitQuery()
            .Include(entry => entry.User)
            .Include(entry => entry.Client)
            .Include(entry => entry.ProjectTask)
            .Include(entry => entry.TimeEntryTags)
                .ThenInclude(tag => tag.Tag)
            .Include($"{nameof(TimeEntry.Project)}.{nameof(Project.Client)}")
            .ToListAsync(cancellationToken);

        var overtimeContext = loadOvertimeContext
            ? await LoadOvertimeContextAsync(entries, cancellationToken)
            : Array.Empty<TimeEntry>();

        var userIds = (loadOvertimeContext ? overtimeContext.Select(e => e.UserId) : entries.Select(e => e.UserId))
            .Distinct()
            .ToList();

        var userRates = userIds.Count == 0
            ? []
            : await _db.UserHourlyRates
                .AsNoTracking()
                .Where(rate => userIds.Contains(rate.UserId))
                .ToListAsync(cancellationToken);

        var window = WeekWindow.Covering(entries.Select(ResolveEntryDate));
        var holidayQuery = _db.Holidays
            .AsNoTracking()
            .Where(holiday => holiday.IsActive);

        if (window is { } covered)
        {
            holidayQuery = holidayQuery.Where(
                holiday => holiday.Date >= covered.FirstWeekStart
                    && holiday.Date <= covered.LastWeekEnd);
        }

        var holidays = entries.Count == 0
            ? []
            : (await holidayQuery
                .Select(holiday => holiday.Date)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        return new ReportEntryData(
            normalized,
            entries,
            overtimeContext,
            userRates,
            holidays,
            await _multipliers.GetAsync(cancellationToken),
            OvertimeContextLoaded: loadOvertimeContext);
    }

    public async Task<IReadOnlyList<TimeEntry>> LoadOvertimeContextAsync(
        IReadOnlyList<TimeEntry> selectedEntries,
        CancellationToken cancellationToken = default)
    {
        var window = WeekWindow.Covering(selectedEntries.Select(ResolveEntryDate));
        if (window is null)
            return [];

        var userIds = selectedEntries
            .Select(entry => entry.UserId)
            .Distinct()
            .ToList();
        var covered = window.Value;

        return await BaseEntryQuery()
            .Where(entry => userIds.Contains(entry.UserId))
            .Where(entry =>
                (entry.StartedAtUtc ?? entry.CreatedAtUtc) >= covered.StartUtc
                && (entry.StartedAtUtc ?? entry.CreatedAtUtc) < covered.EndExclusiveUtc)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<TimeEntry> BaseEntryQuery() =>
        _db.TimeEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.Status == TimeEntryStatus.Confirmed && entry.DeletedAtUtc == null);

    private static IQueryable<TimeEntry> ApplyFilters(
        IQueryable<TimeEntry> entries,
        ReportQuery query)
    {
        if (query.UserIds.Count > 0)
            entries = entries.Where(entry => query.UserIds.Contains(entry.UserId));

        if (query.ProjectIds.Count > 0)
            entries = entries.Where(
                entry => entry.ProjectId.HasValue && query.ProjectIds.Contains(entry.ProjectId.Value));

        if (query.ClientIds.Count > 0)
        {
            entries = entries.Where(entry =>
                (entry.ClientId.HasValue && query.ClientIds.Contains(entry.ClientId.Value))
                || (entry.Project != null
                    && query.ClientIds.Contains(entry.Project.ClientId)));
        }

        if (query.TaskIds.Count > 0)
            entries = entries.Where(
                entry => entry.ProjectTaskId.HasValue && query.TaskIds.Contains(entry.ProjectTaskId.Value));

        if (query.TagIds.Count > 0)
            entries = entries.Where(
                entry => entry.TimeEntryTags.Any(tag => query.TagIds.Contains(tag.TagId)));

        if (query.Billable is { } billable)
            entries = entries.Where(entry => entry.IsBillable == billable);

        if (query.From is { } from)
        {
            var fromUtc = TimesheetWeek.ToUtcMidnight(from);
            entries = entries.Where(
                entry => (entry.StartedAtUtc ?? entry.CreatedAtUtc) >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toExclusiveUtc = to == DateOnly.MaxValue
                ? DateTime.MaxValue
                : TimesheetWeek.ToUtcMidnight(to.AddDays(1));
            entries = entries.Where(
                entry => (entry.StartedAtUtc ?? entry.CreatedAtUtc) < toExclusiveUtc);
        }

        return entries;
    }

    private static DateOnly ResolveEntryDate(TimeEntry entry) =>
        DateOnly.FromDateTime(entry.StartedAtUtc ?? entry.CreatedAtUtc);
}

public sealed record ReportEntryData(
    ReportQuery Query,
    IReadOnlyList<TimeEntry> Entries,
    IReadOnlyList<TimeEntry> OvertimeContext,
    IReadOnlyList<UserHourlyRate> UserRates,
    IReadOnlySet<DateOnly> Holidays,
    RateMultiplierConfig MultiplierConfig,
    bool OvertimeContextLoaded = true);
