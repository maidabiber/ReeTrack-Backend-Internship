namespace ReeTrack.Application.Common.Models;

/// <summary>
/// Employee workload divided across clients and projects (RT-52).
/// Fixed shape — not driven by <see cref="ReportQuery.GroupBy"/>.
/// </summary>
public sealed class WorkloadReportDto
{
    public required ReportKpisDto Kpis { get; init; }
    public required ReportBasisDto Basis { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public string? GeneratedByName { get; init; }
    public DateOnly? FirstEntryDate { get; init; }
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }

    /// <summary>
    /// One row per member × client × project. Sorted by member hours desc, then client/project name.
    /// </summary>
    public required IReadOnlyList<WorkloadAllocationDto> Allocations { get; init; }

    public required long GrandTotalSeconds { get; init; }
    public required long GrandTotalBillableSeconds { get; init; }

    /// <summary>Overtime / weekend / holiday rollups for the filtered set.</summary>
    public required IReadOnlyList<WorkloadScheduleDto> Schedule { get; init; }
}

public sealed class WorkloadAllocationDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public Guid? ClientId { get; init; }
    public required string ClientName { get; init; }
    public Guid? ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }

    /// <summary>Share of this member's total hours on a 0–100 scale.</summary>
    public required decimal PctOfMemberTotal { get; init; }
}

public sealed class WorkloadScheduleDto
{
    public required string Label { get; init; }
    public required decimal Hours { get; init; }
    public required decimal PctOfTotalHours { get; init; }
}
