namespace ReeTrack.Application.Reports;

/// <summary>How a project bills its client. Rendering lives in the presentation layer.</summary>
public enum ProjectBillingModel
{
    None,
    Hourly,
    FixedFee
}

/// <summary>Cost rollup for one currency code. Amounts are never summed across codes.</summary>
public sealed record CostByCurrencyInsight(
    string CurrencyCode,
    int ProjectCount,
    decimal TotalCost,
    long TotalSeconds,
    decimal AvgCostPerHour,
    string TopProjectName,
    decimal TopProjectCost);

/// <summary>
/// Spend for one currency split into mutually exclusive hour-type buckets;
/// Normal + Weekend + Holiday + Overtime equals TotalCost.
/// </summary>
public sealed record CostByHourTypeInsight(
    string CurrencyCode,
    decimal NormalCost,
    decimal WeekendCost,
    decimal HolidayCost,
    decimal OvertimeCost,
    decimal TotalCost);

/// <summary>Portfolio rollup for one schedule category (overtime / weekend / holiday).</summary>
public sealed record ScheduleCategoryInsight(
    string Label,
    decimal Hours,
    decimal PctOfTotalHours,
    string? TopProjectName,
    decimal TopProjectHours);
