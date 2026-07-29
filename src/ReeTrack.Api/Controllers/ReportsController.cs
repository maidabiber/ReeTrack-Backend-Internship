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
    private readonly IReportFilterSetService _filterSets;

    public ReportsController(
        IReportService reports,
        IReportExportService export,
        IReportFilterSetService filterSets)
    {
        _reports = reports;
        _export = export;
        _filterSets = filterSets;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryReportResponse>> GetSummary(
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken)
    {
        var summary = await _reports.GetSummaryAsync(MapQuery(query), cancellationToken);
        return Ok(Map(summary));
    }

    [HttpGet("summary/export")]
    public async Task<IActionResult> ExportSummary(
        [FromQuery] string format,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken)
    {
        if (!TryParseFormat(format, out var parsed))
            throw new AppException("format must be csv, xlsx, or pdf.", 400);

        var file = await _export.ExportSummaryAsync(parsed, MapQuery(query), cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("filter-sets")]
    public async Task<ActionResult<PagedResult<ReportFilterSetResponse>>> ListFilterSets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _filterSets.ListAsync(page, pageSize, cancellationToken);
        return Ok(new PagedResult<ReportFilterSetResponse>
        {
            Items = result.Items.Select(MapFilterSet).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpPost("filter-sets")]
    public async Task<ActionResult<ReportFilterSetResponse>> CreateFilterSet(
        [FromBody] SaveReportFilterSetRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Query is null)
            throw new AppException("Report filter query is required.", 400);

        var created = await _filterSets.CreateAsync(
            request.Name,
            MapQuery(request.Query),
            cancellationToken);
        return Ok(MapFilterSet(created));
    }

    [HttpPut("filter-sets/{id:guid}")]
    public async Task<ActionResult<ReportFilterSetResponse>> UpdateFilterSet(
        Guid id,
        [FromBody] SaveReportFilterSetRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Query is null)
            throw new AppException("Report filter query is required.", 400);

        var updated = await _filterSets.UpdateAsync(
            id,
            request.Name,
            MapQuery(request.Query),
            cancellationToken);
        return Ok(MapFilterSet(updated));
    }

    [HttpDelete("filter-sets/{id:guid}")]
    public async Task<IActionResult> DeleteFilterSet(Guid id, CancellationToken cancellationToken)
    {
        await _filterSets.DeleteAsync(id, cancellationToken);
        return NoContent();
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
            FilterFromDate = dto.FilterFromDate,
            FilterToDate = dto.FilterToDate,
            GeneratedByName = dto.GeneratedByName,
            Basis = new ReportBasisResponse
            {
                WeekendPremium = dto.Basis.WeekendPremium,
                HolidayPremium = dto.Basis.HolidayPremium,
                OvertimePremium = dto.Basis.OvertimePremium,
                WeeklyOvertimeThresholdHours = dto.Basis.WeeklyOvertimeThresholdHours
            }
        };

    private static ReportQuery MapQuery(ReportQueryRequest request) =>
        new()
        {
            UserIds = request.UserIds ?? [],
            ProjectIds = request.ProjectIds ?? [],
            ClientIds = request.ClientIds ?? [],
            TaskIds = request.TaskIds ?? [],
            TagIds = request.TagIds ?? [],
            Billable = request.Billable,
            From = request.From,
            To = request.To,
            GroupBy = (request.GroupBy ?? []).Select(ParseGroupBy).ToList()
        };

    private static ReportGroupBy ParseGroupBy(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "user" => ReportGroupBy.User,
            "project" => ReportGroupBy.Project,
            "client" => ReportGroupBy.Client,
            "task" => ReportGroupBy.Task,
            "tag" => ReportGroupBy.Tag,
            "billable" => ReportGroupBy.Billable,
            "day" => ReportGroupBy.Day,
            "week" => ReportGroupBy.Week,
            _ => throw new AppException(
                "GroupBy must contain only: user, project, client, task, tag, billable, day, or week.",
                400)
        };

    private static ReportFilterSetResponse MapFilterSet(ReportFilterSetDto filterSet) =>
        new()
        {
            Id = filterSet.Id,
            Name = filterSet.Name,
            Query = new ReportQueryResponse
            {
                UserIds = filterSet.Query.UserIds,
                ProjectIds = filterSet.Query.ProjectIds,
                ClientIds = filterSet.Query.ClientIds,
                TaskIds = filterSet.Query.TaskIds,
                TagIds = filterSet.Query.TagIds,
                Billable = filterSet.Query.Billable,
                From = filterSet.Query.From,
                To = filterSet.Query.To,
                GroupBy = filterSet.Query.GroupBy
                    .Select(group => group.ToString().ToLowerInvariant())
                    .ToList()
            },
            SchemaVersion = filterSet.SchemaVersion,
            CreatedAtUtc = filterSet.CreatedAtUtc,
            UpdatedAtUtc = filterSet.UpdatedAtUtc
        };
}
