using System.Globalization;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Shared human-readable formatting for CSV / Excel / PDF report exports.
/// Durations are always rendered as hours — never raw seconds.
///
/// Formatting only: the arithmetic behind these strings lives in
/// <see cref="SummaryReportAnalytics"/> so the API and the exports share one definition.
/// </summary>
public static class ReportFormat
{
    /// <inheritdoc cref="SummaryReportAnalytics.Hours"/>
    public static decimal Hours(long seconds) => SummaryReportAnalytics.Hours(seconds);

    /// <summary>Human label: "40h 30m", "45m", "0m".</summary>
    public static string HoursLabel(long seconds)
    {
        var safe = Math.Max(0, seconds);
        var hours = safe / 3600;
        var minutes = (safe % 3600) / 60;
        if (hours == 0)
            return $"{minutes}m";
        if (minutes == 0)
            return $"{hours}h";
        return $"{hours}h {minutes}m";
    }

    /// <summary>Label for a decimal-hours KPI (e.g. OvertimeHours from the calculator).</summary>
    public static string HoursLabel(decimal hours) =>
        HoursLabel((long)Math.Round(hours * 3600m, MidpointRounding.AwayFromZero));

    /// <summary>Plain hours number with up to 2 decimals (e.g. an estimate "40" or "37.5").</summary>
    public static string Hours2(decimal hours) =>
        hours.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Percent already on a 0–100 scale → "62.5%".</summary>
    public static string Percent(decimal percent) =>
        $"{percent.ToString("0.##", CultureInfo.InvariantCulture)}%";

    /// <summary>Currency-formatted amount with ISO code; never sums across codes.</summary>
    public static string Money(decimal amount, string currencyCode)
    {
        var code = string.IsNullOrWhiteSpace(currencyCode) ? "" : currencyCode.Trim().ToUpperInvariant();
        var formatted = amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(code) ? formatted : $"{formatted} {code}";
    }

    public static string FriendlyDate(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string FriendlyDateTime(DateTime utc) =>
        utc.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>Compact axis label for the PDF sparkline only — no year, so never use it in data columns.</summary>
    public static string FriendlyWeek(DateOnly weekStart) =>
        weekStart.ToString("d MMM", CultureInfo.InvariantCulture);

    /// <summary>Sortable, unambiguous date for machine-readable columns.</summary>
    public static string IsoDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Row label for confirmed time that isn't linked to a project.</summary>
    public const string UnassignedLabel = "Unassigned";

    /// <summary>
    /// The inclusive UTC date window the report covers.
    /// </summary>
    public static string PeriodLabel(SummaryReportDto model)
    {
        if (model.FilterFromDate is { } from && model.FilterToDate is { } to)
            return $"{FriendlyDate(from)} – {FriendlyDate(to)}";

        if (model.FilterFromDate is { } fromOnly)
            return $"Since {FriendlyDate(fromOnly)}";

        if (model.FilterToDate is { } toOnly)
            return $"Through {FriendlyDate(toOnly)}";

        return model.FirstEntryDate is { } first
            ? $"All time · {FriendlyDate(first)} – {FriendlyDate(DateOnly.FromDateTime(model.GeneratedAtUtc))}"
            : "All time";
    }

    /// <summary>
    /// The rules behind the figures, one statement per line. Every export carries these:
    /// weekend / holiday / overtime money is indefensible without the premiums that
    /// produced it, and the confirmed-only and UTC caveats change what the totals mean.
    /// </summary>
    public static IReadOnlyList<string> BasisLines(SummaryReportDto model)
    {
        var basis = model.Basis;
        return
        [
            "Confirmed time entries only; pending and rejected time is excluded.",
            $"Weekend +{Multiplier(basis.WeekendPremium)}, holiday +{Multiplier(basis.HolidayPremium)}, " +
            $"overtime +{Multiplier(basis.OvertimePremium)} above {Hours2(basis.WeeklyOvertimeThresholdHours)}h " +
            "per person per week.",
            "Cost is internal labour cost from member hourly rates, not client revenue.",
            "Amounts are never summed across currencies.",
            "Days, weekends and holidays are determined in UTC."
        ];
    }

    /// <summary>A premium fraction as a percentage, e.g. 0.5 → "50%".</summary>
    private static string Multiplier(decimal premium) =>
        Percent(Math.Round(premium * 100m, 2, MidpointRounding.AwayFromZero));

    /// <summary>"Fixed fee", "Hourly", or "—" — the display label for a billing model.</summary>
    public static string BillingModelLabel(decimal? hourlyRate, decimal? fixedFeeAmount) =>
        SummaryReportAnalytics.BillingModel(hourlyRate, fixedFeeAmount) switch
        {
            ProjectBillingModel.FixedFee => "Fixed fee",
            ProjectBillingModel.Hourly => "Hourly",
            _ => "—"
        };

    /// <summary>
    /// One-line highlights derived from the summary DTO, e.g.
    /// "Team logged 312h across 7 projects (64% billable). …"
    /// </summary>
    public static string Highlights(SummaryReportDto model) =>
        string.Join(' ', HighlightLines(model));

    /// <summary>
    /// Highlights as separate lines. The PDF renders them as bullets; CSV and Excel
    /// join them into their single Highlights cell via <see cref="Highlights"/>.
    /// </summary>
    public static IReadOnlyList<string> HighlightLines(SummaryReportDto model)
    {
        var kpis = model.Kpis;
        var totalLabel = HoursLabel(kpis.TotalSeconds);
        var billable = Percent(kpis.BillablePct);
        var projectWord = kpis.ActiveProjects == 1 ? "project" : "projects";

        var busiest = model.Activity
            .OrderByDescending(d => d.TotalSeconds)
            .ThenBy(d => d.DayOfWeek, StringComparer.Ordinal)
            .FirstOrDefault();

        var top = model.Projects.FirstOrDefault();

        var parts = new List<string>
        {
            $"Team logged {totalLabel} across {kpis.ActiveProjects} {projectWord} ({billable} billable)."
        };

        if (busiest is { TotalSeconds: > 0 })
            parts.Add($"Busiest day: {busiest.DayOfWeek}.");

        if (top is not null && top.TotalSeconds > 0)
        {
            var share = Percent(SummaryReportAnalytics.PctOfTotal(top.TotalSeconds, kpis.TotalSeconds));
            parts.Add($"Top project: {top.Name} ({share} of hours).");
        }

        if (kpis.OvertimeHours > 0)
            parts.Add($"Overtime: {HoursLabel(kpis.OvertimeHours)}.");

        if (kpis.WeekendHours > 0)
            parts.Add($"Weekend: {HoursLabel(kpis.WeekendHours)}.");

        if (kpis.HolidayHours > 0)
            parts.Add($"Holiday: {HoursLabel(kpis.HolidayHours)}.");

        var overEstimate = model.Projects.Count(p => SummaryReportAnalytics.EstimateUsedPct(p.TotalSeconds, p.TimeEstimateHours) is > 100m);
        if (overEstimate > 0)
            parts.Add($"{overEstimate} {(overEstimate == 1 ? "project" : "projects")} over time estimate.");

        var hourType = SummaryReportAnalytics.CostByHourType(model).FirstOrDefault();
        if (hourType is not null && hourType.TotalCost > 0)
        {
            parts.Add(
                $"Spend: {Money(hourType.NormalCost, hourType.CurrencyCode)} normal, " +
                $"{Money(hourType.WeekendCost, hourType.CurrencyCode)} weekend" +
                (hourType.HolidayCost > 0 || hourType.OvertimeCost > 0
                    ? $", {Money(hourType.HolidayCost, hourType.CurrencyCode)} holiday, {Money(hourType.OvertimeCost, hourType.CurrencyCode)} overtime"
                    : "") +
                $" ({hourType.CurrencyCode}).");
        }
        else
        {
            var costLines = CostInsightLines(model);
            if (costLines.Count > 0)
                parts.Add(costLines[0]);
        }

        return parts;
    }

    /// <summary>
    /// Per-currency cost rollups (never summed across codes) plus highest-cost project.
    /// </summary>
    public static IReadOnlyList<string> CostInsightLines(SummaryReportDto model)
    {
        var byCurrency = SummaryReportAnalytics.CostByCurrency(model);
        if (byCurrency.Count == 0)
            return [];

        var lines = new List<string>();

        foreach (var group in byCurrency)
        {
            var projectWord = group.ProjectCount == 1 ? "project" : "projects";
            lines.Add(
                $"{Money(group.TotalCost, group.CurrencyCode)} across {group.ProjectCount} {projectWord}" +
                (group.AvgCostPerHour > 0 ? $" (~{Money(group.AvgCostPerHour, group.CurrencyCode)}/h)." : "."));
        }

        // Highest cost across every currency. Comparing raw amounts across codes is not
        // meaningful, but this is a single "look here" pointer, not an aggregate.
        var highest = byCurrency
            .OrderByDescending(g => g.TopProjectCost)
            .ThenBy(g => g.TopProjectName, StringComparer.OrdinalIgnoreCase)
            .First();
        if (highest.TopProjectCost > 0)
            lines.Add($"Highest cost: {highest.TopProjectName} ({Money(highest.TopProjectCost, highest.CurrencyCode)}).");

        return lines;
    }
}
