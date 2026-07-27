using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly IReportExportService _export;

    public ReportsController(IReportService reports, IReportExportService export)
    {
        _reports = reports;
        _export = export;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryReportResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _reports.GetSummaryAsync(cancellationToken);
        return Ok(Map(summary));
    }

    [HttpGet("summary/export")]
    public async Task<IActionResult> ExportSummary(
        [FromQuery] string format,
        CancellationToken cancellationToken)
    {
        if (!TryParseFormat(format, out var parsed))
            throw new AppException("format must be csv, xlsx, or pdf.", 400);

        var file = await _export.ExportSummaryAsync(parsed, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    private static bool TryParseFormat(string? format, out ReportExportFormat parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(format))
            return false;

        switch (format.Trim().ToLowerInvariant())
        {
            case "csv":
                parsed = ReportExportFormat.Csv;
                return true;
            case "xlsx":
            case "excel":
                parsed = ReportExportFormat.Xlsx;
                return true;
            case "pdf":
                parsed = ReportExportFormat.Pdf;
                return true;
            default:
                return false;
        }
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
                HolidayHours = dto.Kpis.HolidayHours,
                UnassignedSeconds = dto.Kpis.UnassignedSeconds
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
                    NormalCost = p.NormalCost,
                    WeekendCost = p.WeekendCost,
                    HolidayCost = p.HolidayCost,
                    OvertimeCost = p.OvertimeCost,
                    OvertimeHours = p.OvertimeHours,
                    WeekendHours = p.WeekendHours,
                    HolidayHours = p.HolidayHours,
                    ClientName = p.ClientName,
                    Status = p.Status,
                    HourlyRate = p.HourlyRate,
                    FixedFeeAmount = p.FixedFeeAmount,
                    TimeEstimateHours = p.TimeEstimateHours,
                    EstimateUsedPct = SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours),
                    FixedFeeMargin = SummaryReportAnalytics.FixedFeeMargin(p.FixedFeeAmount, p.CalculatedCost)
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
            GeneratedAtUtc = dto.GeneratedAtUtc,
            FirstEntryDate = dto.FirstEntryDate,
            GeneratedByName = dto.GeneratedByName,
            Basis = new ReportBasisResponse
            {
                WeekendPremium = dto.Basis.WeekendPremium,
                HolidayPremium = dto.Basis.HolidayPremium,
                OvertimePremium = dto.Basis.OvertimePremium,
                WeeklyOvertimeThresholdHours = dto.Basis.WeeklyOvertimeThresholdHours
            }
        };
}
