using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

// Trust-based domain: every authenticated user may create/edit/delete tags
// (no Admin role gate on mutations). Changes are captured by the audit trail
// and deletes are soft-deletes that keep historical time-entry associations.
[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> List(CancellationToken cancellationToken)
    {
        try
        {
            var tags = await _tagService.ListAsync(cancellationToken);
            return Ok(tags.Select(MapTag).ToList());
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TagResponse>> Create(
        [FromBody] CreateTagRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tag = await _tagService.CreateAsync(request?.Name, request?.Color, cancellationToken);
            return Ok(MapTag(tag));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TagResponse>> Update(
        Guid id,
        [FromBody] UpdateTagRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tag = await _tagService.UpdateAsync(id, request?.Name, request?.Color, cancellationToken);
            return Ok(MapTag(tag));
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _tagService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (AppException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    internal static TagResponse MapTag(TagDto tag) =>
        new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            UsageCount = tag.UsageCount,
            CreatedAtUtc = tag.CreatedAtUtc
        };
}

public sealed class CreateTagRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

public sealed class UpdateTagRequest
{
    public string? Name { get; set; }

    // Sentinel: omit to leave the color unchanged; send "" to clear it.
    public string? Color { get; set; }
}

public sealed class TagResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string? Color { get; init; }
    public required int UsageCount { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
