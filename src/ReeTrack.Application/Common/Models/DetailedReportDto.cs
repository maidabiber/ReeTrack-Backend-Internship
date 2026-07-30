namespace ReeTrack.Application.Common.Models;

/// <summary>
/// Entry-level audit report — filtered KPIs plus a paginated (or full) entry list.
/// Cost buckets use the same rules as <see cref="SummaryReportDto"/> project rows.
/// </summary>
public sealed class DetailedReportDto
{
    public required ReportKpisDto Kpis { get; init; }
    public required ReportBasisDto Basis { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public string? GeneratedByName { get; init; }
    public DateOnly? FirstEntryDate { get; init; }
    public DateOnly? FilterFromDate { get; init; }
    public DateOnly? FilterToDate { get; init; }

    /// <summary>Current page of entries (or the full set when exporting).</summary>
    public required IReadOnlyList<DetailedEntryDto> Entries { get; init; }

    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    /// <summary>Section headers/subtotals when <see cref="ReportQuery.GroupBy"/> is set; otherwise empty.</summary>
    public required IReadOnlyList<DetailedGroupDto> Groups { get; init; }
}

public sealed class DetailedEntryDto
{
    public required Guid EntryId { get; init; }
    public required DateOnly EntryDate { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public Guid? TaskId { get; init; }
    public string? TaskName { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public string? Description { get; init; }
    public required bool IsBillable { get; init; }
    public required long DurationSeconds { get; init; }
    public string? CurrencyCode { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required decimal NormalCost { get; init; }
    public required decimal WeekendCost { get; init; }
    public required decimal HolidayCost { get; init; }
    public required decimal OvertimeCost { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal WeekendHours { get; init; }
    public required decimal HolidayHours { get; init; }
    public required bool IsWeekend { get; init; }
    public required bool IsHoliday { get; init; }
}

public sealed class DetailedGroupDto
{
    /// <summary>Human-readable section label, e.g. "Acme / Design".</summary>
    public required string Label { get; init; }

    /// <summary>Dimension values that produced this group (ordered as GroupBy).</summary>
    public required IReadOnlyList<string> Keys { get; init; }

    public required long TotalSeconds { get; init; }
    public required decimal CalculatedCost { get; init; }
    public required int EntryCount { get; init; }

    /// <summary>Inclusive index range into the sorted full entry list (not the page).</summary>
    public required int StartIndex { get; init; }
    public required int EndIndexExclusive { get; init; }
}
