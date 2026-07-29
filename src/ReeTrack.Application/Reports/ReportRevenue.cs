namespace ReeTrack.Application.Reports;

public static class ReportRevenue
{
    /// <summary>
    /// Fixed-fee projects recognize the full fee when the filtered period has activity.
    /// Hourly projects recognize only billable time at the project billing rate.
    /// </summary>
    public static decimal Calculate(
        decimal? hourlyRate,
        decimal? fixedFeeAmount,
        long totalSeconds,
        long billableSeconds)
    {
        if (fixedFeeAmount is > 0m)
            return totalSeconds > 0 ? Round(fixedFeeAmount.Value) : 0m;

        if (hourlyRate is not > 0m || billableSeconds <= 0)
            return 0m;

        return Round(billableSeconds / 3600m * hourlyRate.Value);
    }

    public static decimal Margin(decimal revenue, decimal labourCost) =>
        Round(revenue - labourCost);

    public static decimal? MarginPct(decimal revenue, decimal labourCost) =>
        revenue <= 0m
            ? null
            : Math.Round((revenue - labourCost) * 100m / revenue, 2, MidpointRounding.AwayFromZero);

    private static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
