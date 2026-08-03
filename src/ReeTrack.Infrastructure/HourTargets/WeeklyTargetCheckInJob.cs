using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications;
using ReeTrack.Application.Notifications.Events;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.HourTargets;

public sealed class WeeklyTargetCheckInJob : IWeeklyTargetCheckInJob
{
    private readonly IApplicationDbContext _db;
    private readonly IHourTargetSettingsService _settingsService;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<WeeklyTargetCheckInJob> _logger;

    public WeeklyTargetCheckInJob(
        IApplicationDbContext db,
        IHourTargetSettingsService settingsService,
        IDomainEventPublisher eventPublisher,
        ILogger<WeeklyTargetCheckInJob> logger)
    {
        _db = db;
        _settingsService = settingsService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task RunAsync(
        DateTime utcNow,
        WeeklyTargetCheckInOptions options,
        CancellationToken cancellationToken = default)
    {
        var timeZone = ResolveTimeZone(options.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);
        var todayLocal = DateOnly.FromDateTime(localNow);
        var weekStartLocal = TimesheetWeek.ToWeekStart(todayLocal);

        var alreadyRan = await _db.WeeklyTargetCheckInRuns
            .AsNoTracking()
            .AnyAsync(r => r.WeekStartDate == weekStartLocal, cancellationToken);

        if (alreadyRan)
        {
            _logger.LogDebug(
                "Weekly target check-in already recorded for week starting {WeekStart}.",
                weekStartLocal);
            return;
        }

        var weekEndLocal = weekStartLocal.AddDays(6);
        var rangeStartLocal = weekStartLocal.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var rangeStartUtc = TimeZoneInfo.ConvertTimeToUtc(rangeStartLocal, timeZone);
        var rangeEndUtc = utcNow;

        var holidayFrom = weekStartLocal.AddDays(-7);
        var holidayTo = weekEndLocal.AddDays(7);
        var holidays = (await _db.Holidays
                .AsNoTracking()
                .Where(h => h.IsActive && h.Date >= holidayFrom && h.Date <= holidayTo)
                .Select(h => h.Date)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var orgDefaults = await _settingsService.GetAsync(cancellationToken);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var overrides = await _db.UserHourTargets
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId))
            .ToDictionaryAsync(t => t.UserId, cancellationToken);

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e =>
                userIds.Contains(e.UserId)
                && e.DeletedAtUtc == null
                && e.StartedAtUtc != null
                && e.StartedAtUtc >= rangeStartUtc
                && e.StartedAtUtc < rangeEndUtc)
            .Select(e => new { e.UserId, e.StartedAtUtc, e.DurationSeconds })
            .ToListAsync(cancellationToken);

        var entriesByUser = entries.GroupBy(e => e.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var timesheetWeekStart = TimesheetWeek.ToWeekStart(rangeStartUtc);

        foreach (var user in users)
        {
            try
            {
                HourTargetMode mode;
                decimal targetHours;
                if (overrides.TryGetValue(user.Id, out var userOverride))
                {
                    mode = userOverride.Mode;
                    targetHours = userOverride.TargetHours;
                }
                else
                {
                    mode = orgDefaults.Mode;
                    targetHours = orgDefaults.TargetHours;
                }

                var loggedByDate = new Dictionary<DateOnly, int>();
                if (entriesByUser.TryGetValue(user.Id, out var userEntries))
                {
                    foreach (var entry in userEntries)
                    {
                        var startedUtc = DateTime.SpecifyKind(entry.StartedAtUtc!.Value, DateTimeKind.Utc);
                        var localDate = DateOnly.FromDateTime(
                            TimeZoneInfo.ConvertTimeFromUtc(startedUtc, timeZone));
                        loggedByDate[localDate] =
                            loggedByDate.GetValueOrDefault(localDate) + entry.DurationSeconds;
                    }
                }

                var progress = WeeklyTargetProgressCalculator.Calculate(
                    mode,
                    targetHours,
                    loggedByDate,
                    weekStartLocal,
                    todayLocal,
                    holidays);

                var recipientName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.Email
                    : user.DisplayName!;

                await _eventPublisher.PublishAsync(
                    new WeeklyTargetCheckInNotification
                    {
                        RecipientUserId = user.Id,
                        RecipientName = recipientName,
                        LoggedHours = progress.LoggedHours,
                        TargetHours = progress.TargetHours,
                        RemainingHours = progress.RemainingHours,
                        OnTrack = progress.OnTrack,
                        WeekStartDate = weekStartLocal,
                        TimesheetWeekStartDate = timesheetWeekStart,
                        WeakestDay = progress.WeakestDay,
                        WeakestDayHours = progress.WeakestDay is null ? null : progress.WeakestDayHours
                    },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Weekly target check-in failed for user {UserId}.",
                    user.Id);
            }
        }

        try
        {
            var now = DateTime.UtcNow;
            _db.WeeklyTargetCheckInRuns.Add(new WeeklyTargetCheckInRun
            {
                Id = Guid.NewGuid(),
                WeekStartDate = weekStartLocal,
                RanAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Another instance likely recorded the same week; treat as success.
            _logger.LogInformation(
                ex,
                "Weekly target check-in run row already exists for week starting {WeekStart}.",
                weekStartLocal);
        }

        _logger.LogInformation(
            "Weekly target check-in completed for week starting {WeekStart} ({UserCount} users).",
            weekStartLocal,
            users.Count);
    }

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
