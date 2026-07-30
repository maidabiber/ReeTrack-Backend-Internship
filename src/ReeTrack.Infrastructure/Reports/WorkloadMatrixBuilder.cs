using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>
/// Builds member × client × project hour allocations for the workload report.
/// </summary>
internal static class WorkloadMatrixBuilder
{
    public static (
        IReadOnlyList<WorkloadAllocationDto> Allocations,
        long GrandTotalSeconds,
        long GrandTotalBillableSeconds)
        Build(IReadOnlyList<TimeEntry> entries)
    {
        if (entries.Count == 0)
            return ([], 0, 0);

        var memberTotals = entries
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(e => (long)e.DurationSeconds));

        var allocations = entries
            .GroupBy(e => (
                e.UserId,
                ClientId: ReportMetadataResolver.ResolveClientId(e),
                ProjectId: e.ProjectId))
            .Select(g =>
            {
                var sample = g.First();
                var user = sample.User;
                var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
                    ? user?.Email ?? g.Key.UserId.ToString()
                    : user.DisplayName;
                var total = g.Sum(e => (long)e.DurationSeconds);
                var billable = g.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds);
                var memberTotal = memberTotals[g.Key.UserId];

                return new WorkloadAllocationDto
                {
                    UserId = g.Key.UserId,
                    DisplayName = displayName,
                    ClientId = g.Key.ClientId,
                    ClientName = ReportMetadataResolver.ResolveClientName(sample) ?? "(No client)",
                    ProjectId = g.Key.ProjectId,
                    ProjectName = sample.Project?.Name ?? "(Unassigned)",
                    TotalSeconds = total,
                    BillableSeconds = billable,
                    PctOfMemberTotal = SummaryReportAnalytics.PctOfTotal(total, memberTotal)
                };
            })
            .OrderByDescending(a => memberTotals[a.UserId])
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.ClientName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grandTotal = allocations.Sum(a => a.TotalSeconds);
        var grandBillable = allocations.Sum(a => a.BillableSeconds);
        return (allocations, grandTotal, grandBillable);
    }
}
