namespace ReeTrack.Api.Contracts;

public sealed class SaveReportFilterSetRequest
{
    public string? Name { get; init; }
    public ReportQueryRequest? Query { get; init; }
}

public sealed class ReportFilterSetResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ReportQueryResponse Query { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

public sealed class ReportQueryResponse
{
    public required IReadOnlyList<Guid> UserIds { get; init; }
    public required IReadOnlyList<Guid> ProjectIds { get; init; }
    public required IReadOnlyList<Guid> ClientIds { get; init; }
    public required IReadOnlyList<Guid> TaskIds { get; init; }
    public required IReadOnlyList<Guid> TagIds { get; init; }
    public bool? Billable { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public required IReadOnlyList<string> GroupBy { get; init; }
}
