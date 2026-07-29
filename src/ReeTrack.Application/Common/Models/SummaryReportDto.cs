namespace ReeTrack.Application.Common.Models;

public sealed class SummaryReportDto
{
    public required ReportKpisDto Kpis { get; init; }

    /// <summary>Monday → Sunday, always seven entries.</summary>
    public required IReadOnlyList<DayOfWeekHoursDto> Activity { get; init; }

    /// <summary>Oldest week first, zero-filled, ending at the in-progress week.</summary>
    public required IReadOnlyList<TrendPointDto> WeeklyTrend { get; init; }

    /// <summary>Ranked: most hours first, ties broken by name. Consumers must not re-sort.</summary>
    public required IReadOnlyList<ProjectSummaryDto> Projects { get; init; }

    /// <summary>Ranked: most hours first, ties broken by display name. Consumers must not re-sort.</summary>
    public required IReadOnlyList<MemberHoursDto> Members { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// Date of the earliest confirmed entry, or null when there is none. The report
    /// covers all time; this is what the "since …" period label is built from.
    /// </summary>
    public DateOnly? FirstEntryDate { get; init; }

    /// <summary>Inclusive UTC date filters applied to this report.</summary>
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }

    /// <summary>Display name of the admin who ran the report; null when unresolvable.</summary>
    public string? GeneratedByName { get; init; }

    /// <summary>The rules the figures were produced under. Required to defend them.</summary>
    public required ReportBasisDto Basis { get; init; }
}

/// <summary>
/// What the numbers were computed from. The report quotes weekend, holiday and
/// overtime money without these it is impossible to check, so every export states them.
/// </summary>
public sealed class ReportBasisDto
{
    /// <summary>Weekend premium as a fraction, e.g. 0.5 = +50%.</summary>
    public required decimal WeekendPremium { get; init; }
    public required decimal HolidayPremium { get; init; }
    public required decimal OvertimePremium { get; init; }

    /// <summary>Hours per person per calendar week after which overtime applies.</summary>
    public required decimal WeeklyOvertimeThresholdHours { get; init; }
}

public sealed class ReportKpisDto
{
    public required long TotalSeconds { get; init; }
    public required long BillableSeconds { get; init; }
    public required long NonBillableSeconds { get; init; }
    public required decimal BillablePct { get; init; }
    public required int EntryCount { get; init; }
    public required int ActiveMembers { get; init; }
    public required int ActiveProjects { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }

    /// <summary>
    /// Confirmed time not linked to any project. Excluded from <see cref="SummaryReportDto.Projects"/>
    /// by definition, so project rows only reconcile to <see cref="TotalSeconds"/> once this is added back.
    /// </summary>
    public required long UnassignedSeconds { get; init; }
}

public sealed class DayOfWeekHoursDto
{
    /// <summary>Day name, e.g. "Monday" … "Sunday".</summary>
    public required string DayOfWeek { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class TrendPointDto
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class ProjectSummaryDto
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string CurrencyCode { get; init; }
    public required long TotalSeconds { get; init; }
    public required decimal CalculatedCost { get; init; }
    /// <summary>Mutually exclusive cost buckets — sum equals <see cref="CalculatedCost"/>.</summary>
    public required decimal NormalCost { get; init; }
    public required decimal WeekendCost { get; init; }
    public required decimal HolidayCost { get; init; }
    public required decimal OvertimeCost { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }

    // Billing / planning context (optional — a project may set neither rate nor estimate).
    public string ClientName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal? HourlyRate { get; init; }
    public decimal? FixedFeeAmount { get; init; }
    public decimal? TimeEstimateHours { get; init; }
}

public sealed class MemberHoursDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}
