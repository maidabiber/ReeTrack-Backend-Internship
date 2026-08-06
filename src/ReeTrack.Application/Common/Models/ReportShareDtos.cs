using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Models;

public sealed class CreateShareLinkRequest
{
    public required ReportShareReportType ReportType { get; init; }
    public Guid? ReportId { get; init; }
    public string? QueryJson { get; init; }
    public string? SpecJson { get; init; }
    public required ReportShareAccessLevel AccessLevel { get; init; }
    public IReadOnlyList<Guid>? RecipientUserIds { get; init; }
}

public sealed class ShareLinkDto
{
    public required Guid Id { get; init; }
    public required string Token { get; init; }
    public required string Url { get; init; }
    public required ReportShareReportType ReportType { get; init; }
    public required ReportShareAccessLevel AccessLevel { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public int RecipientCount { get; init; }
    public string? QueryJson { get; init; }
}

public sealed class SharedReportDto
{
    public required ReportShareReportType ReportType { get; init; }
    public required ReportShareAccessLevel AccessLevel { get; init; }
    public SummaryReportDto? Summary { get; init; }
    public DetailedReportDto? Detailed { get; init; }
    public WorkloadReportDto? Workload { get; init; }
    public ProfitabilityReportDto? Profitability { get; init; }
    public CustomReportDto? Custom { get; init; }
}
