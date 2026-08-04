using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.Reports.Custom;

/// <summary>
/// Flat projection of one confirmed entry for custom-report aggregation.
/// Dimension keys and cost figures are resolved once here so block evaluators
/// never re-walk navigation properties.
/// </summary>
internal sealed record EntryRow(
    Guid EntryId,
    Guid UserId,
    string UserName,
    Guid? ProjectId,
    string ProjectLabel,
    Guid? ClientId,
    string ClientLabel,
    Guid? TaskId,
    string TaskLabel,
    IReadOnlyList<(Guid Id, string Label)> Tags,
    bool IsBillable,
    DateOnly Date,
    DateOnly WeekStart,
    string CurrencyCode,
    long DurationSeconds,
    string? Description,
    EntryCostLine? Cost);
