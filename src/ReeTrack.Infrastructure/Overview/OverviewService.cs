using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Reports;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Overview;

public sealed class OverviewService : IOverviewService
{
    public static readonly TimeSpan StaleTimerThreshold = TimeSpan.FromHours(4);
    private const int TopProjectsLimit = 5;
    private const int IdleMembersLimit = 20;

    private readonly IApplicationDbContext _db;
    private readonly IReportService _reports;
    private readonly ICurrentUserService _currentUser;

    public OverviewService(
        IApplicationDbContext db,
        IReportService reports,
        ICurrentUserService currentUser)
    {
        _db = db;
        _reports = reports;
        _currentUser = currentUser;
    }

    public async Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var isAdmin = _currentUser.Roles.Contains(RoleNames.Admin);
        var isPm = _currentUser.Roles.Contains(RoleNames.ProjectManager) && !isAdmin;

        // Sequential: shared DbContext is not safe for concurrent queries.
        var summary = await _reports.GetSummaryAsync(
            new ReportQuery { From = today, To = today },
            cancellationToken);

        // TODO: Replace with a ProjectMembers table once introduced. Currently PM scoping
        // uses Project.CreatedByUserId, matching the existing "Mine" filter on the project list.
        HashSet<Guid>? pmProjectIds = null;
        if (isPm)
        {
            pmProjectIds = await _db.Projects
                .AsNoTracking()
                .Where(p => p.CreatedByUserId == _currentUser.UserId)
                .Select(p => p.Id)
                .ToHashSetAsync(cancellationToken);
        }

        var activeTimers = await LoadActiveTimersAsync(now, cancellationToken);

        if (isPm && pmProjectIds is not null)
        {
            activeTimers = activeTimers
                .Where(t => t.ProjectId is not null && pmProjectIds.Contains(t.ProjectId.Value))
                .ToList();
        }

        var activeUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.AvatarUrl })
            .ToListAsync(cancellationToken);

        var onClockUserIds = activeTimers.Select(t => t.UserId).ToHashSet();
        var loggedUserIds = summary.Members
            .Where(m => m.TotalSeconds > 0)
            .Select(m => m.UserId)
            .ToHashSet();

        var idle = activeUsers
            .Where(u => !onClockUserIds.Contains(u.Id) && !loggedUserIds.Contains(u.Id))
            .OrderBy(u => string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .Select(u => new IdleMemberOverviewDto
            {
                UserId = u.Id,
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName!,
                AvatarUrl = u.AvatarUrl
            })
            .ToList();

        var membersLogged = summary.Members.Count(m => m.TotalSeconds > 0);

        var topProjects = summary.Projects
            .Take(TopProjectsLimit)
            .Select(p => new OverviewProjectHoursDto
            {
                ProjectId = p.ProjectId,
                Name = p.Name,
                TotalSeconds = p.TotalSeconds
            })
            .ToList();

        if (isPm && pmProjectIds is not null)
        {
            topProjects = topProjects
                .Where(p => pmProjectIds.Contains(p.ProjectId))
                .ToList();
        }

        // Today-scoped digest pieces (projects / members / weekly trend KPIs).
        var digestProjects = summary.Projects
            .Where(p => p.TotalSeconds > 0)
            .Select(p => new OverviewProjectDigestDto
            {
                ProjectId = p.ProjectId,
                Name = p.Name,
                Color = null,
                TotalSeconds = p.TotalSeconds,
                BillablePct = p.TotalSeconds > 0
                    ? Math.Round((decimal)p.TotalSeconds / p.TotalSeconds * 100, 1)
                    : 0,
                CalculatedCost = p.CalculatedCost,
                Currency = p.CurrencyCode,
                ClientName = p.ClientName,
                Status = p.Status,
                TimeEstimateHours = p.TimeEstimateHours
            })
            .ToList();

        if (isPm && pmProjectIds is not null)
        {
            digestProjects = digestProjects
                .Where(p => pmProjectIds.Contains(p.ProjectId))
                .ToList();
        }

        var digestMembers = summary.Members
            .Where(m => m.TotalSeconds > 0)
            .Select(m => new OverviewMemberDigestDto
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                TotalSeconds = m.TotalSeconds
            })
            .ToList();

        var weekActivity = await LoadWeekActivityAsync(today, pmProjectIds, cancellationToken);

        return new AdminOverviewDto
        {
            GeneratedAtUtc = now,
            Scope = isAdmin ? "workspace" : "owned",
            Today = new OverviewTodayKpisDto
            {
                Date = today,
                TotalSeconds = summary.Kpis.TotalSeconds,
                BillableSeconds = summary.Kpis.BillableSeconds,
                BillablePct = summary.Kpis.BillablePct,
                EntryCount = summary.Kpis.EntryCount,
                MembersLogged = membersLogged,
                UnassignedSeconds = summary.Kpis.UnassignedSeconds
            },
            OnTheClock = activeTimers.Count,
            ActiveTimers = activeTimers,
            IdleMembers = idle.Take(IdleMembersLimit).ToList(),
            IdleCount = idle.Count,
            TopProjects = topProjects,
            Digest = new OverviewDigestDto
            {
                Activity = weekActivity,
                WeeklyTrend = summary.WeeklyTrend
                    .Select(t => new OverviewWeeklyTrendDto
                    {
                        Week = t.WeekStartDate.ToString("yyyy-MM-dd"),
                        Seconds = t.TotalSeconds,
                        Status = ""
                    })
                    .ToList(),
                OvertimeSeconds = (long)(summary.Kpis.OvertimeHours * 3600),
                WeekendSeconds = (long)(summary.Kpis.WeekendHours * 3600),
                HolidaySeconds = (long)(summary.Kpis.HolidayHours * 3600),
                Projects = digestProjects,
                Members = digestMembers
            }
        };
    }

    /// <summary>
    /// Confirmed hours Mon–Sun (UTC) of the current week, zero-filled.
    /// PM viewers are limited to projects they created.
    /// </summary>
    private async Task<IReadOnlyList<OverviewDailySecondsDto>> LoadWeekActivityAsync(
        DateOnly today,
        HashSet<Guid>? pmProjectIds,
        CancellationToken cancellationToken)
    {
        var weekStart = TimesheetWeek.ToWeekStart(today);
        var fromUtc = TimesheetWeek.ToUtcMidnight(weekStart);
        var toExclusiveUtc = TimesheetWeek.ToUtcMidnight(weekStart.AddDays(7));

        var rows = await _db.TimeEntries
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.Status == TimeEntryStatus.Confirmed && e.DeletedAtUtc == null)
            .Where(e => (e.StartedAtUtc ?? e.CreatedAtUtc) >= fromUtc
                && (e.StartedAtUtc ?? e.CreatedAtUtc) < toExclusiveUtc)
            .Select(e => new
            {
                Instant = e.StartedAtUtc ?? e.CreatedAtUtc,
                e.DurationSeconds,
                e.ProjectId
            })
            .ToListAsync(cancellationToken);

        if (pmProjectIds is not null)
        {
            rows = rows
                .Where(r => r.ProjectId is not null && pmProjectIds.Contains(r.ProjectId.Value))
                .ToList();
        }

        var activity = ReportAggregations.BuildActivity(
            rows.Select(r => (DateOnly.FromDateTime(r.Instant).DayOfWeek, (long)r.DurationSeconds)));

        return activity
            .Select(a => new OverviewDailySecondsDto
            {
                Day = ToShortDayLabel(a.DayOfWeek),
                Seconds = a.TotalSeconds
            })
            .ToList();
    }

    private static string ToShortDayLabel(string dayOfWeek) =>
        dayOfWeek switch
        {
            "Monday" => "Mon",
            "Tuesday" => "Tue",
            "Wednesday" => "Wed",
            "Thursday" => "Thu",
            "Friday" => "Fri",
            "Saturday" => "Sat",
            "Sunday" => "Sun",
            _ => dayOfWeek.Length >= 3 ? dayOfWeek[..3] : dayOfWeek
        };

    private async Task<IReadOnlyList<ActiveTimerOverviewDto>> LoadActiveTimersAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var timers = await _db.TimeEntries
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Project)
            .Include(e => e.ProjectTask)
            .Where(e => e.Mode == TimeEntryMode.Timer && e.EndedAtUtc == null)
            .OrderBy(e => e.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return timers
            .Select(e =>
            {
                var started = e.StartedAtUtc ?? e.CreatedAtUtc;
                var isUnassigned = e.ProjectId is null;
                var isStale = nowUtc - started >= StaleTimerThreshold;
                var user = e.User;
                var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
                    ? user?.Email ?? e.UserId.ToString()
                    : user.DisplayName!;

                return new ActiveTimerOverviewDto
                {
                    TimeEntryId = e.Id,
                    UserId = e.UserId,
                    DisplayName = displayName,
                    AvatarUrl = user?.AvatarUrl,
                    StartedAtUtc = started,
                    Description = e.Description,
                    IsBillable = e.IsBillable,
                    ProjectId = e.ProjectId,
                    ProjectName = e.Project?.Name,
                    ProjectColor = e.Project?.Color,
                    ProjectTaskId = e.ProjectTaskId,
                    ProjectTaskName = e.ProjectTask?.Name,
                    IsUnassigned = isUnassigned,
                    IsStale = isStale
                };
            })
            .ToList();
    }
}
