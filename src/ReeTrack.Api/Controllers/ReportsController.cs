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
    public Task<IActionResult> ExportSummary(
        [FromQuery] string format,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken) =>
        ExportReport(format, query, (f, q, ct) => _export.ExportSummaryAsync(f, q, ct), cancellationToken);

    [HttpGet("detailed")]
    public async Task<ActionResult<DetailedReportResponse>> GetDetailed(
        [FromQuery] ReportQueryRequest query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            throw AppErrors.Validation("page must be >= 1.");
        if (pageSize < 1 || pageSize > 200)
            throw AppErrors.Validation("pageSize must be between 1 and 200.");

        var detailed = await _reports.GetDetailedAsync(
            MapQuery(query),
            page,
            pageSize,
            cancellationToken);
        return Ok(MapDetailed(detailed));
    }

    [HttpGet("detailed/export")]
    public Task<IActionResult> ExportDetailed(
        [FromQuery] string format,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken) =>
        ExportReport(format, query, (f, q, ct) => _export.ExportDetailedAsync(f, q, ct), cancellationToken);

    [HttpGet("workload")]
    public async Task<ActionResult<WorkloadReportResponse>> GetWorkload(
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken)
    {
        var workload = await _reports.GetWorkloadAsync(MapQuery(query), cancellationToken);
        return Ok(MapWorkload(workload));
    }

    [HttpGet("workload/export")]
    public Task<IActionResult> ExportWorkload(
        [FromQuery] string format,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken) =>
        ExportReport(format, query, (f, q, ct) => _export.ExportWorkloadAsync(f, q, ct), cancellationToken);

    [HttpGet("profitability")]
    public async Task<ActionResult<ProfitabilityReportResponse>> GetProfitability(
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken)
    {
        var profitability = await _reports.GetProfitabilityAsync(MapQuery(query), cancellationToken);
        return Ok(MapProfitability(profitability));
    }

    [HttpGet("profitability/export")]
    public Task<IActionResult> ExportProfitability(
        [FromQuery] string format,
        [FromQuery] ReportQueryRequest query,
        CancellationToken cancellationToken) =>
        ExportReport(format, query, (f, q, ct) => _export.ExportProfitabilityAsync(f, q, ct), cancellationToken);

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
            throw AppErrors.Validation("Report filter query is required.");

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
            throw AppErrors.Validation("Report filter query is required.");

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

    private async Task<IActionResult> ExportReport(
        string format,
        ReportQueryRequest query,
        Func<ReportExportFormat, ReportQuery, CancellationToken, Task<ReportFile>> exportFn,
        CancellationToken cancellationToken)
    {
        if (!TryParseFormat(format, out var parsed))
            throw new AppException("format must be csv, xlsx, or pdf.", 400, ErrorCode.ExportFormatInvalid);

        var file = await exportFn(parsed, MapQuery(query), cancellationToken);
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

    private static DetailedReportResponse MapDetailed(DetailedReportDto dto) =>
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
            Basis = new ReportBasisResponse
            {
                WeekendPremium = dto.Basis.WeekendPremium,
                HolidayPremium = dto.Basis.HolidayPremium,
                OvertimePremium = dto.Basis.OvertimePremium,
                WeeklyOvertimeThresholdHours = dto.Basis.WeeklyOvertimeThresholdHours
            },
            GeneratedAtUtc = dto.GeneratedAtUtc,
            GeneratedByName = dto.GeneratedByName,
            FirstEntryDate = dto.FirstEntryDate,
            FilterFromDate = dto.FilterFromDate,
            FilterToDate = dto.FilterToDate,
            Entries = dto.Entries.Select(MapDetailedEntry).ToList(),
            Page = dto.Page,
            PageSize = dto.PageSize,
            TotalCount = dto.TotalCount,
            Groups = dto.Groups
                .Select(g => new DetailedGroupResponse
                {
                    Label = g.Label,
                    Keys = g.Keys,
                    TotalSeconds = g.TotalSeconds,
                    CalculatedCost = g.CalculatedCost,
                    EntryCount = g.EntryCount,
                    StartIndex = g.StartIndex,
                    EndIndexExclusive = g.EndIndexExclusive
                })
                .ToList()
        };

    private static DetailedEntryResponse MapDetailedEntry(DetailedEntryDto entry) =>
        new()
        {
            EntryId = entry.EntryId,
            EntryDate = entry.EntryDate,
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            UserId = entry.UserId,
            DisplayName = entry.DisplayName,
            ClientId = entry.ClientId,
            ClientName = entry.ClientName,
            ProjectId = entry.ProjectId,
            ProjectName = entry.ProjectName,
            TaskId = entry.TaskId,
            TaskName = entry.TaskName,
            Tags = entry.Tags,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            DurationSeconds = entry.DurationSeconds,
            CurrencyCode = entry.CurrencyCode,
            CalculatedCost = entry.CalculatedCost,
            NormalCost = entry.NormalCost,
            WeekendCost = entry.WeekendCost,
            HolidayCost = entry.HolidayCost,
            OvertimeCost = entry.OvertimeCost,
            OvertimeHours = entry.OvertimeHours,
            WeekendHours = entry.WeekendHours,
            HolidayHours = entry.HolidayHours,
            IsWeekend = entry.IsWeekend,
            IsHoliday = entry.IsHoliday
        };

    private static WorkloadReportResponse MapWorkload(WorkloadReportDto dto) =>
        new()
        {
            Kpis = MapKpis(dto.Kpis),
            Basis = MapBasis(dto.Basis),
            GeneratedAtUtc = dto.GeneratedAtUtc,
            GeneratedByName = dto.GeneratedByName,
            FirstEntryDate = dto.FirstEntryDate,
            FilterFromDate = dto.FilterFromDate,
            FilterToDate = dto.FilterToDate,
            Allocations = dto.Allocations
                .Select(a => new WorkloadAllocationResponse
                {
                    UserId = a.UserId,
                    DisplayName = a.DisplayName,
                    ClientId = a.ClientId,
                    ClientName = a.ClientName,
                    ProjectId = a.ProjectId,
                    ProjectName = a.ProjectName,
                    TotalSeconds = a.TotalSeconds,
                    BillableSeconds = a.BillableSeconds,
                    PctOfMemberTotal = a.PctOfMemberTotal
                })
                .ToList(),
            GrandTotalSeconds = dto.GrandTotalSeconds,
            GrandTotalBillableSeconds = dto.GrandTotalBillableSeconds,
            Schedule = dto.Schedule
                .Select(s => new WorkloadScheduleResponse
                {
                    Label = s.Label,
                    Hours = s.Hours,
                    PctOfTotalHours = s.PctOfTotalHours
                })
                .ToList()
        };

    private static ProfitabilityReportResponse MapProfitability(ProfitabilityReportDto dto) =>
        new()
        {
            Kpis = MapKpis(dto.Kpis),
            Basis = MapBasis(dto.Basis),
            GeneratedAtUtc = dto.GeneratedAtUtc,
            GeneratedByName = dto.GeneratedByName,
            FirstEntryDate = dto.FirstEntryDate,
            FilterFromDate = dto.FilterFromDate,
            FilterToDate = dto.FilterToDate,
            ByCurrency = dto.ByCurrency
                .Select(c => new CurrencyFinancialKpisResponse
                {
                    CurrencyCode = c.CurrencyCode,
                    Revenue = c.Revenue,
                    Cost = c.Cost,
                    Margin = c.Margin,
                    MarginPct = c.MarginPct,
                    BillableHours = c.BillableHours,
                    TotalSeconds = c.TotalSeconds,
                    ProjectCount = c.ProjectCount
                })
                .ToList(),
            WeeklyTrend = dto.WeeklyTrend
                .Select(t => new WeeklyFinancialTrendResponse
                {
                    WeekStartDate = t.WeekStartDate,
                    CurrencyCode = t.CurrencyCode,
                    Revenue = t.Revenue,
                    Cost = t.Cost,
                    Margin = t.Margin
                })
                .ToList(),
            Projects = dto.Projects
                .Select(p => new ProjectProfitabilityResponse
                {
                    ProjectId = p.ProjectId,
                    Name = p.Name,
                    CurrencyCode = p.CurrencyCode,
                    ClientName = p.ClientName,
                    Status = p.Status,
                    BillingModel = p.BillingModel,
                    HourlyRate = p.HourlyRate,
                    FixedFeeAmount = p.FixedFeeAmount,
                    TimeEstimateHours = p.TimeEstimateHours,
                    EstimateUsedPct = p.EstimateUsedPct,
                    TotalSeconds = p.TotalSeconds,
                    BillableSeconds = p.BillableSeconds,
                    Revenue = p.Revenue,
                    CalculatedCost = p.CalculatedCost,
                    NormalCost = p.NormalCost,
                    WeekendCost = p.WeekendCost,
                    HolidayCost = p.HolidayCost,
                    OvertimeCost = p.OvertimeCost,
                    Margin = p.Margin,
                    MarginPct = p.MarginPct
                })
                .ToList(),
            Members = dto.Members
                .Select(m => new MemberLabourCostResponse
                {
                    UserId = m.UserId,
                    DisplayName = m.DisplayName,
                    CurrencyCode = m.CurrencyCode,
                    TotalSeconds = m.TotalSeconds,
                    LabourCost = m.LabourCost
                })
                .ToList(),
            RevenueBasisLines = dto.RevenueBasisLines
        };

    private static ReportKpisResponse MapKpis(ReportKpisDto kpis) =>
        new()
        {
            TotalSeconds = kpis.TotalSeconds,
            BillableSeconds = kpis.BillableSeconds,
            NonBillableSeconds = kpis.NonBillableSeconds,
            BillablePct = kpis.BillablePct,
            EntryCount = kpis.EntryCount,
            ActiveMembers = kpis.ActiveMembers,
            ActiveProjects = kpis.ActiveProjects,
            OvertimeHours = kpis.OvertimeHours,
            WeekendHours = kpis.WeekendHours,
            HolidayHours = kpis.HolidayHours,
            UnassignedSeconds = kpis.UnassignedSeconds
        };

    private static ReportBasisResponse MapBasis(ReportBasisDto basis) =>
        new()
        {
            WeekendPremium = basis.WeekendPremium,
            HolidayPremium = basis.HolidayPremium,
            OvertimePremium = basis.OvertimePremium,
            WeeklyOvertimeThresholdHours = basis.WeeklyOvertimeThresholdHours
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
            _ => throw AppErrors.Validation(
                "GroupBy must contain only: user, project, client, task, tag, billable, day, or week.")
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
