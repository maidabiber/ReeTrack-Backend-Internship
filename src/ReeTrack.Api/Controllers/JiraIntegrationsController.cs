using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Application.Integrations.Jira.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/integrations/jira")]
[Authorize]
public class JiraIntegrationsController : ControllerBase
{
    private readonly IJiraIntegrationService _jira;

    public JiraIntegrationsController(IJiraIntegrationService jira)
    {
        _jira = jira;
    }

    [HttpGet]
    public async Task<ActionResult<JiraConnectionResponse>> GetConnection(CancellationToken cancellationToken)
    {
        var connection = await _jira.GetConnectionAsync(cancellationToken);
        return Ok(MapConnection(connection));
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<JiraRemoteProjectResponse>>> ListProjects(
        CancellationToken cancellationToken)
    {
        var projects = await _jira.ListRemoteProjectsAsync(cancellationToken);
        return Ok(projects.Select(MapRemoteProject).ToList());
    }

    [HttpPost("projects/integrate")]
    public async Task<ActionResult<IntegrateJiraProjectResponse>> Integrate(
        [FromBody] IntegrateJiraProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _jira.IntegrateProjectAsync(
            new IntegrateJiraProjectInput(request.JiraProjectId, request.ClientId),
            userId,
            cancellationToken);

        return Ok(MapIntegrateResult(result));
    }

    [HttpPost("projects/{projectId:guid}/sync")]
    public async Task<ActionResult<IntegrateJiraProjectResponse>> SyncProject(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _jira.SyncProjectAsync(projectId, cancellationToken);
        return Ok(MapIntegrateResult(result));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(claim, out userId);
    }

    private static JiraConnectionResponse MapConnection(JiraConnectionDto connection) =>
        new()
        {
            IsConfigured = connection.IsConfigured,
            SiteUrl = connection.SiteUrl,
            Email = connection.Email
        };

    private static JiraRemoteProjectResponse MapRemoteProject(JiraRemoteProjectDto project) =>
        new()
        {
            Id = project.Id,
            Key = project.Key,
            Name = project.Name,
            IsIntegrated = project.IsIntegrated,
            ReeTrackProjectId = project.ReeTrackProjectId,
            ClientId = project.ClientId,
            ClientName = project.ClientName
        };

    private static IntegrateJiraProjectResponse MapIntegrateResult(IntegrateJiraProjectResult result) =>
        new()
        {
            ProjectId = result.ProjectId,
            ProjectName = result.ProjectName,
            TasksImported = result.TasksImported,
            Message = result.Message
        };
}
