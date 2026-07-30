namespace ReeTrack.Infrastructure.Reports;

/// <summary>
/// <see cref="ReeTrack.Domain.Services.EntryCostLine"/> carries unrounded cost/hours by
/// design (see its doc comment) — every caller rounds once, here, at the point a figure
/// is about to be shown to a user, rather than rounding upstream and drifting on re-sum.
/// </summary>
internal static class ReportRounding
{
    public static decimal Cost(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal Hours(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
