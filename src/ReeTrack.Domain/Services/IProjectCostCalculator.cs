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
}
