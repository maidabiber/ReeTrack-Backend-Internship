using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/time-entry-suggestions")]
[Authorize]
public class TimeEntrySuggestionsController : ControllerBase
{
    private readonly ITimeEntrySuggestionService _suggestionService;

    public TimeEntrySuggestionsController(ITimeEntrySuggestionService suggestionService)
    {
        _suggestionService = suggestionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimeEntrySuggestionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var suggestions = await _suggestionService.GetSuggestionsAsync(cancellationToken);
        return Ok(suggestions.Select(MapSuggestion).ToList());
    }

    internal static TimeEntrySuggestionResponse MapSuggestion(TimeEntrySuggestionDto suggestion) =>
        new()
        {
            ClientId = suggestion.ClientId,
            ProjectId = suggestion.ProjectId,
            ProjectTaskId = suggestion.ProjectTaskId,
            IsBillable = suggestion.IsBillable,
            SuggestedDescription = suggestion.SuggestedDescription,
            SuggestedStartTimeUtc = suggestion.SuggestedStartTimeUtc,
            SuggestedEndTimeUtc = suggestion.SuggestedEndTimeUtc,
            DurationSeconds = suggestion.DurationSeconds,
            Score = suggestion.Score,
            ProjectName = suggestion.ProjectName,
            ProjectColor = suggestion.ProjectColor,
            ProjectTaskName = suggestion.ProjectTaskName
        };
}
