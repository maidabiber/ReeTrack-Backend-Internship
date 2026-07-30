namespace ReeTrack.Application.Common.Models;

/// <summary>
/// Revenue / labour cost / margin report (RT-53). Amounts are never summed across currencies.
/// </summary>
public sealed class ProfitabilityReportDto
{
    public required ReportKpisDto Kpis { get; init; }
    public required ReportBasisDto Basis { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public string? GeneratedByName { get; init; }
    public DateOnly? FirstEntryDate { get; init; }
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }

    /// <summary>KPIs rolled up per currency (never cross-summed).</summary>
    public required IReadOnlyList<CurrencyFinancialKpisDto> ByCurrency { get; init; }

    /// <summary>
    /// Weekly revenue/cost/margin per currency, zero-filled for the recent window.
    /// Fixed-fee revenue is attributed once to the first week with activity in range.
    /// </summary>
    public required IReadOnlyList<WeeklyFinancialTrendDto> WeeklyTrend { get; init; }

    /// <summary>Projects ranked by margin descending (ties by name).</summary>
    public required IReadOnlyList<ProjectProfitabilityDto> Projects { get; init; }

    /// <summary>Member labour-cost rollups per currency (not payroll).</summary>
    public required IReadOnlyList<MemberLabourCostDto> Members { get; init; }

    /// <summary>Explicit revenue recognition rules for Basis / exports.</summary>
    public required IReadOnlyList<string> RevenueBasisLines { get; init; }
}

public sealed class CurrencyFinancialKpisDto
{
    public required string CurrencyCode { get; init; }
    public required decimal Revenue { get; init; }
    public required decimal Cost { get; init; }
    public required decimal Margin { get; init; }
    public decimal? MarginPct { get; init; }
    public required decimal BillableHours { get; init; }
    public required long TotalSeconds { get; init; }
    public required int ProjectCount { get; init; }
}

public sealed class WeeklyFinancialTrendDto
{
    public required DateOnly WeekStartDate { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal Revenue { get; init; }
    public required decimal Cost { get; init; }
    public required decimal Margin { get; init; }
}

public sealed class ProjectProfitabilityDto
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string CurrencyCode { get; init; }
    public required string ClientName { get; init; }
    public required string Status { get; init; }
    public required string BillingModel { get; init; }
    public decimal? HourlyRate { get; init; }
    public decimal? FixedFeeAmount { get; init; }
    public decimal? TimeEstimateHours { get; init; }
    public decimal? EstimateUsedPct { get; init; }
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    public required decimal Revenue { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal NormalCost { get; init; }
    public required decimal WeekendCost { get; init; }
    public required decimal HolidayCost { get; init; }
    public required decimal OvertimeCost { get; init; }
    public required decimal Margin { get; init; }
    public decimal? MarginPct { get; init; }
}

public sealed class MemberLabourCostDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string CurrencyCode { get; init; }
    public required long TotalSeconds { get; init; }
    public required decimal LabourCost { get; init; }
}
