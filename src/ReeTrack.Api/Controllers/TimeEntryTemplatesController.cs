using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/time-entry-templates")]
[Authorize]
public class TimeEntryTemplatesController : ControllerBase
{
    private readonly ITimeEntryTemplateService _templateService;

    public TimeEntryTemplatesController(ITimeEntryTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TimeEntryTemplateResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _templateService.ListAsync(page, pageSize, cancellationToken);
        return Ok(new PagedResult<TimeEntryTemplateResponse>
        {
            Items = result.Items.Select(MapTemplate).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpPost]
    public async Task<ActionResult<TimeEntryTemplateResponse>> Create(
        [FromBody] CreateTimeEntryTemplateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.TimeEntryId == Guid.Empty)
            return BadRequest(new { message = "TimeEntryId is required." });

        var template = await _templateService.CreateFromTimeEntryAsync(
            request.TimeEntryId,
            cancellationToken);
        return Ok(MapTemplate(template));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _templateService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    internal static TimeEntryTemplateResponse MapTemplate(TimeEntryTemplateDto template) =>
        new()
        {
            Id = template.Id,
            TimeEntryId = template.TimeEntryId,
            ProjectId = template.ProjectId,
            ProjectTaskId = template.ProjectTaskId,
            Description = template.Description,
            IsBillable = template.IsBillable,
            StartTimeUtc = template.StartTimeUtc,
            EndTimeUtc = template.EndTimeUtc,
            DurationSeconds = template.DurationSeconds,
            CreatedAtUtc = template.CreatedAtUtc,
            ProjectName = template.ProjectName,
            ProjectColor = template.ProjectColor,
            ProjectTaskName = template.ProjectTaskName,
            Tags = template.Tags
                .Select(tag => new TimeEntryTagResponse
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Color = tag.Color
                })
                .ToList()
        };
}
