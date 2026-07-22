using ReeTrack.Domain.Entities;

namespace ReeTrack.Domain.Services;

public interface IProjectCostCalculator
{
    decimal Calculate(
        Project project,
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<UserHourlyRate> userRates);
}
