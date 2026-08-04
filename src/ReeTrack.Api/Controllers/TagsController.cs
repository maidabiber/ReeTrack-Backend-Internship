using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

// Reads are member-accessible; mutations require manage.projects.manage
// (ProjectManager+). Deletes are soft-deletes that keep historical associations.
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
    public async Task<ActionResult<PagedResult<TagResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tagService.ListAsync(new TagListQuery
        {
            Page = page,
            PageSize = pageSize,
            Q = q
        }, cancellationToken);

        return Ok(new PagedResult<TagResponse>
        {
            Items = result.Items.Select(MapTag).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Policies.ProjectsManage)]
    public async Task<ActionResult<TagResponse>> Create(
        [FromBody] CreateTagRequest? request,
        CancellationToken cancellationToken)
    {
        var tag = await _tagService.CreateAsync(request?.Name, request?.Color, cancellationToken);
        return Ok(MapTag(tag));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.Policies.ProjectsManage)]
    public async Task<ActionResult<TagResponse>> Update(
        Guid id,
        [FromBody] UpdateTagRequest? request,
        CancellationToken cancellationToken)
    {
        var tag = await _tagService.UpdateAsync(id, request?.Name, request?.Color, cancellationToken);
        return Ok(MapTag(tag));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Policies.ProjectsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _tagService.DeleteAsync(id, cancellationToken);
        return NoContent();
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
