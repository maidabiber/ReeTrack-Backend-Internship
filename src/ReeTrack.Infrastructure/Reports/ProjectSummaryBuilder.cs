using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Timesheets;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>Per-project cost/hours rollup shared by the Summary and Profitability reports.</summary>
internal static class ProjectSummaryBuilder
{
    public static IReadOnlyList<ProjectSummaryDto> Build(
        IProjectCostCalculator calculator,
        IReadOnlyList<TimeEntry> selectedEntries,
        IReadOnlyList<TimeEntry> overtimeContext,
        ILookup<Guid, UserHourlyRate> ratesByUser,
        IReadOnlySet<DateOnly> holidays,
        RateMultiplierConfig multiplierConfig)
    {
        var projectGroups = selectedEntries
            .Where(e => e.ProjectId is not null && e.Project is not null)
            .GroupBy(e => e.ProjectId!.Value)
            .ToList();

        // Index once instead of rescanning the whole portfolio per project: the window
        // slice below was O(projects × entries) and allocated a fresh list each pass.
        // Order is irrelevant — ProjectCostCalculator sorts by instant itself.
        var entriesByUser = overtimeContext
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<ProjectSummaryDto>(projectGroups.Count);

        foreach (var group in projectGroups)
        {
            var project = group.First().Project!;
            var projectEntries = group.ToList();
            var userIds = projectEntries.Select(e => e.UserId).Distinct().ToHashSet();

            // Same cross-project week window as ProjectCostService, sliced from the
            // already-loaded portfolio set (no per-project queries).
            // A GroupBy group is never empty, so the window always resolves.
            var window = WeekWindow.Covering(projectEntries.Select(ReportMetadataResolver.ResolveEntryDate))!.Value;
            var crossProjectUserEntries = userIds
                .SelectMany(id => entriesByUser[id])
                .Where(e => window.Contains(ReportMetadataResolver.ResolveEntryInstant(e)))
                .ToList();

            var projectRates = userIds
                .SelectMany(id => ratesByUser[id])
                .ToList();

            var cost = calculator.Calculate(
                project,
                projectEntries,
                crossProjectUserEntries,
                projectRates,
                holidays,
                multiplierConfig);

            results.Add(new ProjectSummaryDto
            {
                ProjectId = project.Id,
                Name = project.Name,
                CurrencyCode = project.CurrencyCode,
                ClientName = project.Client?.Name ?? string.Empty,
                Status = project.Status.ToString(),
                HourlyRate = project.HourlyRate,
                FixedFeeAmount = project.FixedFeeAmount,
                TimeEstimateHours = project.TimeEstimateHours,
                TotalSeconds = projectEntries.Sum(e => (long)e.DurationSeconds),
                CalculatedCost = cost.CalculatedCost,
                NormalCost = cost.NormalCost,
                WeekendCost = cost.WeekendCost,
                HolidayCost = cost.HolidayCost,
                OvertimeCost = cost.OvertimeCost,
                OvertimeHours = cost.OvertimeHours,
                WeekendHours = cost.WeekendHours,
                HolidayHours = cost.HolidayHours
            });
        }

        return results
            .OrderByDescending(p => p.TotalSeconds)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
