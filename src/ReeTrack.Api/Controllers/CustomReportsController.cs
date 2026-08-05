using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/reports/custom")]
[Authorize(Policy = Permissions.Policies.ReportsView)]
public sealed class CustomReportsController : ControllerBase
{
    private readonly ICustomReportService _customReports;
    private readonly ICustomReportDefinitionService _definitions;
    private readonly ICustomReportInsightService _insights;

    public CustomReportsController(
        ICustomReportService customReports,
        ICustomReportDefinitionService definitions,
        ICustomReportInsightService insights)
    {
        _customReports = customReports;
        _definitions = definitions;
        _insights = insights;
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

    /// <summary>
    /// Writes commentary for one narrative block. Separate from /run so a report stays fast,
    /// free, and reproducible; the caller stores the result on the block.
    /// </summary>
    [HttpPost("insights")]
    public async Task<ActionResult<CustomReportInsightsResponse>> GenerateInsights(
        [FromBody] CustomReportInsightsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Spec is null || string.IsNullOrWhiteSpace(request.BlockId))
            throw AppErrors.Validation("A custom report spec and block id are required.");

        var insights = await _insights.GenerateAsync(request.Spec, request.BlockId, cancellationToken);

        return Ok(new CustomReportInsightsResponse
        {
            BlockId = insights.BlockId,
            Paragraphs = insights.Paragraphs,
            GeneratedAtUtc = insights.GeneratedAtUtc,
            Fingerprint = insights.Fingerprint
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string format,
        [FromBody] CustomReportRunRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryParseFormat(format, out var parsed))
            throw new AppException("format must be csv, xlsx, or pdf.", 400, ErrorCode.ExportFormatInvalid);

        // Matches /run: an absent body binds to null before `required` validation runs, which
        // would otherwise be a 500.
        if (request?.Spec is null)
            throw AppErrors.Validation("Custom report spec is required.");

        var file = await _customReports.ExportAsync(request.Spec, parsed, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpGet("definitions")]
    public async Task<ActionResult<PagedResult<CustomReportDefinitionResponse>>> ListDefinitions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? owner = null,
        CancellationToken cancellationToken = default)
    {
        var ownerFilter = ParseOwnerFilter(owner);
        var result = await _definitions.ListAsync(page, pageSize, ownerFilter, cancellationToken);
        return Ok(new PagedResult<CustomReportDefinitionResponse>
        {
            Items = result.Items.Select(MapDefinition).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("definitions/{id:guid}")]
    public async Task<ActionResult<CustomReportDefinitionResponse>> GetDefinition(
        Guid id,
        CancellationToken cancellationToken)
    {
        var definition = await _definitions.GetByIdAsync(id, cancellationToken);
        return Ok(MapDefinition(definition));
    }

    [HttpPost("definitions")]
    public async Task<ActionResult<CustomReportDefinitionResponse>> CreateDefinition(
        [FromBody] SaveCustomReportDefinitionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Spec is null)
            throw AppErrors.Validation("Custom report spec is required.");

        var definition = await _definitions.CreateAsync(
            request.Name,
            request.Description,
            request.Spec,
            request.Visibility,
            cancellationToken);

        return Ok(MapDefinition(definition));
    }

    [HttpPut("definitions/{id:guid}")]
    public async Task<ActionResult<CustomReportDefinitionResponse>> UpdateDefinition(
        Guid id,
        [FromBody] SaveCustomReportDefinitionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Spec is null)
            throw AppErrors.Validation("Custom report spec is required.");

        var definition = await _definitions.UpdateAsync(
            id,
            request.Name,
            request.Description,
            request.Spec,
            request.Visibility,
            cancellationToken);

        return Ok(MapDefinition(definition));
    }

    [HttpPost("definitions/{id:guid}/duplicate")]
    public async Task<ActionResult<CustomReportDefinitionResponse>> DuplicateDefinition(
        Guid id,
        CancellationToken cancellationToken)
    {
        var definition = await _definitions.DuplicateAsync(id, cancellationToken);
        return Ok(MapDefinition(definition));
    }

    [HttpDelete("definitions/{id:guid}")]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken cancellationToken)
    {
        await _definitions.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static CustomReportOwnerFilter? ParseOwnerFilter(string? owner) => owner?.Trim().ToLowerInvariant() switch
    {
        "mine" => CustomReportOwnerFilter.Mine,
        "shared" => CustomReportOwnerFilter.Shared,
        _ => null
    };

    private static CustomReportDefinitionResponse MapDefinition(CustomReportDefinitionDto definition) =>
        new()
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            Spec = definition.Spec,
            SchemaVersion = definition.SchemaVersion,
            CreatedByUserId = definition.CreatedByUserId,
            Visibility = definition.Visibility,
            CreatedAtUtc = definition.CreatedAtUtc,
            UpdatedAtUtc = definition.UpdatedAtUtc,
            CanEdit = definition.CanEdit
        };

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
            Warnings = dto.Warnings,
            Comparison = dto.Comparison
        };
}
