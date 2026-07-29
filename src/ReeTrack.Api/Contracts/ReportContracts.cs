namespace ReeTrack.Api.Contracts;

public sealed class SummaryReportResponse
{
    public required ReportKpisResponse Kpis { get; init; }
    public required IReadOnlyList<DayOfWeekHoursResponse> Activity { get; init; }
    public required IReadOnlyList<TrendPointResponse> WeeklyTrend { get; init; }
    public required IReadOnlyList<ProjectSummaryResponse> Projects { get; init; }
    public required IReadOnlyList<MemberHoursResponse> Members { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>Earliest confirmed entry date; null when there is no time logged.</summary>
    public DateOnly? FirstEntryDate { get; init; }

    /// <summary>Inclusive UTC date filters applied to this report.</summary>
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }

    /// <summary>Display name of the admin who ran the report; null when unresolvable.</summary>
    public string? GeneratedByName { get; init; }

    /// <summary>The rules the figures were produced under.</summary>
    public required ReportBasisResponse Basis { get; init; }
}

public sealed class ReportBasisResponse
{
    /// <summary>Premiums as fractions, e.g. 0.5 = +50%.</summary>
    public required decimal WeekendPremium { get; init; }
    public required decimal HolidayPremium { get; init; }
    public required decimal OvertimePremium { get; init; }
    public required decimal WeeklyOvertimeThresholdHours { get; init; }
}

public sealed class ReportKpisResponse
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

    /// <summary>Confirmed time not linked to a project; excluded from the Projects list.</summary>
    public required long UnassignedSeconds { get; init; }
}

public sealed class DayOfWeekHoursResponse
{
    public required string DayOfWeek { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class TrendPointResponse
{
    public required DateOnly WeekStartDate { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class ProjectSummaryResponse
{
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string CurrencyCode { get; init; }
    public required long TotalSeconds { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal NormalCost { get; init; }
    public required decimal WeekendCost { get; init; }
    public required decimal HolidayCost { get; init; }
    public required decimal OvertimeCost { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required string ClientName { get; init; }
    public required string Status { get; init; }
    public decimal? HourlyRate { get; init; }
    public decimal? FixedFeeAmount { get; init; }
    public decimal? TimeEstimateHours { get; init; }

    // Derived server-side so the page and the exports can't drift apart.
    /// <summary>Logged hours as a percent of the estimate (0–100+); null when no estimate is set.</summary>
    public decimal? EstimateUsedPct { get; init; }

    /// <summary>Fixed fee minus labour cost; null for non-fixed-fee projects.</summary>
    public decimal? FixedFeeMargin { get; init; }
}

public sealed class MemberHoursResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required long TotalSeconds { get; init; }
}

public sealed class ReportQueryRequest
{
    public Guid[]? UserIds { get; init; } = [];
    public Guid[]? ProjectIds { get; init; } = [];
    public Guid[]? ClientIds { get; init; } = [];
    public Guid[]? TaskIds { get; init; } = [];
    public Guid[]? TagIds { get; init; } = [];
    public bool? Billable { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string?[]? GroupBy { get; init; } = [];
}
