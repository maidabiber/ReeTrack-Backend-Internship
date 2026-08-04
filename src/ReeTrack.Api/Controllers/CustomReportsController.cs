using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/reports/custom")]
[Authorize(Roles = "Admin")]
public sealed class CustomReportsController : ControllerBase
{
    private readonly ICustomReportService _customReports;

    public CustomReportsController(ICustomReportService customReports)
    {
        _customReports = customReports;
    }

    [HttpGet("catalogue")]
    public ActionResult<CustomReportCatalogueResponse> GetCatalogue()
    {
        var catalogue = _customReports.GetCatalogue();
        return Ok(new CustomReportCatalogueResponse
        {
            Dimensions = catalogue.Dimensions,
            Metrics = catalogue.Metrics,
            BlockTypes = catalogue.BlockTypes,
            EntryColumns = catalogue.EntryColumns,
            Operators = catalogue.Operators
        });
    }

    [HttpPost("run")]
    public async Task<ActionResult<CustomReportRunResponse>> Run(
        [FromBody] CustomReportRunRequest? request,
        CancellationToken cancellationToken)
    {
        // An absent body binds to null before `required` validation runs, which would be a 500.
        if (request?.Spec is null)
            throw AppErrors.Validation("Custom report spec is required.");

        var report = await _customReports.RunAsync(request.Spec, cancellationToken);
        return Ok(Map(report));
    }

    private static CustomReportRunResponse Map(CustomReportDto dto) =>
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
            Blocks = dto.Blocks,
            Warnings = dto.Warnings
        };
}
