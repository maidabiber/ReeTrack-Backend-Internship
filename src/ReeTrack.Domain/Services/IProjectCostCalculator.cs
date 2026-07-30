using ReeTrack.Domain.Entities;

namespace ReeTrack.Domain.Services;

public interface IProjectCostCalculator
{
    ProjectCostResult Calculate(
        Project project,
        IReadOnlyList<TimeEntry> projectEntries,
        IReadOnlyList<TimeEntry> crossProjectUserEntries,
        IReadOnlyList<UserHourlyRate> userRates,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig);

    /// <summary>
    /// Entry-level cost lines for the detailed report. Project rate is taken from
    /// each entry's loaded <see cref="TimeEntry.Project"/> when present (else 0).
    /// </summary>
    IReadOnlyList<EntryCostLine> CalculateEntries(
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<TimeEntry> crossProjectUserEntries,
        IReadOnlyList<UserHourlyRate> userRates,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig);
}
