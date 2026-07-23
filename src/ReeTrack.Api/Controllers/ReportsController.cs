using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryReportResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _reports.GetSummaryAsync(cancellationToken);
        return Ok(Map(summary));
    }

    private static SummaryReportResponse Map(SummaryReportDto dto) =>
        new()
        {
            Kpis = new ReportKpisResponse
            {
                TotalSeconds = dto.Kpis.TotalSeconds,
                BillableSeconds = dto.Kpis.BillableSeconds,
                NonBillableSeconds = dto.Kpis.NonBillableSeconds,
                BillablePct = dto.Kpis.BillablePct,
                EntryCount = dto.Kpis.EntryCount,
                ActiveMembers = dto.Kpis.ActiveMembers,
                ActiveProjects = dto.Kpis.ActiveProjects,
                OvertimeHours = dto.Kpis.OvertimeHours,
                WeekendHours = dto.Kpis.WeekendHours,
                HolidayHours = dto.Kpis.HolidayHours
            },
            Activity = dto.Activity
                .Select(d => new DayOfWeekHoursResponse
                {
                    DayOfWeek = d.DayOfWeek,
                    TotalSeconds = d.TotalSeconds
                })
                .ToList(),
            WeeklyTrend = dto.WeeklyTrend
                .Select(t => new TrendPointResponse
                {
                    WeekStartDate = t.WeekStartDate,
                    TotalSeconds = t.TotalSeconds
                })
                .ToList(),
            Projects = dto.Projects
                .Select(p => new ProjectSummaryResponse
                {
                    ProjectId = p.ProjectId,
                    Name = p.Name,
                    CurrencyCode = p.CurrencyCode,
                    TotalSeconds = p.TotalSeconds,
                    CalculatedCost = p.CalculatedCost,
                    OvertimeHours = p.OvertimeHours,
                    WeekendHours = p.WeekendHours,
                    HolidayHours = p.HolidayHours
                })
                .ToList(),
            Members = dto.Members
                .Select(m => new MemberHoursResponse
                {
                    UserId = m.UserId,
                    DisplayName = m.DisplayName,
                    TotalSeconds = m.TotalSeconds
                })
                .ToList(),
            GeneratedAtUtc = dto.GeneratedAtUtc
        };
}
