using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntrySuggestionService
{
    /// <summary>
    /// Returns up to 5 suggested time-entry shapes ranked by historical likelihood
    /// for the current user (last 30 days of non-deleted entries).
    /// </summary>
    Task<IReadOnlyList<TimeEntrySuggestionDto>> GetSuggestionsAsync(
        CancellationToken cancellationToken = default);
}
