using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ISmartTimeParseService
{
    /// <summary>
    /// Parses free-form time-entry text with LLM Structured Outputs,
    /// matching against the provided projects, tasks, and tags.
    /// </summary>
    Task<ParsedTimeEntryDto> ParseAsync(
        string userInput,
        SmartTimeParseCatalog catalog,
        CancellationToken cancellationToken = default);
}
