using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/overview")]
[Authorize(Policy = Permissions.Policies.ReportsView)]
public class OverviewController : ControllerBase
{
    private readonly IOverviewService _overview;

    public OverviewController(IOverviewService overview)
    {
        _overview = overview;
    }

    [HttpGet]
    public async Task<ActionResult<AdminOverviewResponse>> Get(CancellationToken cancellationToken)
    {
        var dto = await _overview.GetAsync(cancellationToken);
        return Ok(Map(dto));
    }

    private static AdminOverviewResponse Map(AdminOverviewDto dto) =>
        new()
        {
            GeneratedAtUtc = dto.GeneratedAtUtc,
            Scope = dto.Scope,
            Today = new OverviewTodayKpisResponse
            {
                Date = dto.Today.Date,
                TotalSeconds = dto.Today.TotalSeconds,
                BillableSeconds = dto.Today.BillableSeconds,
                BillablePct = dto.Today.BillablePct,
                EntryCount = dto.Today.EntryCount,
                MembersLogged = dto.Today.MembersLogged,
                UnassignedSeconds = dto.Today.UnassignedSeconds
            },
            OnTheClock = dto.OnTheClock,
            ActiveTimers = dto.ActiveTimers.Select(t => new ActiveTimerOverviewResponse
            {
                TimeEntryId = t.TimeEntryId,
                UserId = t.UserId,
                DisplayName = t.DisplayName,
                AvatarUrl = t.AvatarUrl,
                StartedAtUtc = t.StartedAtUtc,
                Description = t.Description,
                IsBillable = t.IsBillable,
                ProjectId = t.ProjectId,
                ProjectName = t.ProjectName,
                ProjectColor = t.ProjectColor,
                ProjectTaskId = t.ProjectTaskId,
                ProjectTaskName = t.ProjectTaskName,
                IsUnassigned = t.IsUnassigned,
                IsStale = t.IsStale
            }).ToList(),
            IdleMembers = dto.IdleMembers.Select(m => new IdleMemberOverviewResponse
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                AvatarUrl = m.AvatarUrl
            }).ToList(),
            IdleCount = dto.IdleCount,
            TopProjects = dto.TopProjects.Select(p => new OverviewProjectHoursResponse
            {
                ProjectId = p.ProjectId,
                Name = p.Name,
                TotalSeconds = p.TotalSeconds
            }).ToList(),
            Digest = dto.Digest is null ? null : MapDigest(dto.Digest)
        };

    private static OverviewDigestResponse MapDigest(OverviewDigestDto digest) =>
        new()
        {
            Activity = digest.Activity.Select(a => new OverviewDailySecondsResponse
            {
                Day = a.Day,
                Seconds = a.Seconds
            }).ToList(),
            WeeklyTrend = digest.WeeklyTrend.Select(t => new OverviewWeeklyTrendResponse
            {
                Week = t.Week,
                Seconds = t.Seconds,
                Status = t.Status
            }).ToList(),
            OvertimeSeconds = digest.OvertimeSeconds,
            WeekendSeconds = digest.WeekendSeconds,
            HolidaySeconds = digest.HolidaySeconds,
            Projects = digest.Projects.Select(p => new OverviewProjectDigestResponse
            {
                ProjectId = p.ProjectId,
                Name = p.Name,
                Color = p.Color,
                TotalSeconds = p.TotalSeconds,
                BillablePct = p.BillablePct,
                CalculatedCost = p.CalculatedCost,
                Currency = p.Currency,
                ClientName = p.ClientName,
                Status = p.Status,
                TimeEstimateHours = p.TimeEstimateHours
            }).ToList(),
            Members = digest.Members.Select(m => new OverviewMemberDigestResponse
            {
                UserId = m.UserId,
                DisplayName = m.DisplayName,
                TotalSeconds = m.TotalSeconds
            }).ToList()
        };
}
