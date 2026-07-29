namespace ReeTrack.Application.Common.Models;

public sealed class ReportQuery
{
    public IReadOnlyList<Guid> UserIds { get; init; } = [];
    public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
    public IReadOnlyList<Guid> ClientIds { get; init; } = [];
    public IReadOnlyList<Guid> TaskIds { get; init; } = [];
    public IReadOnlyList<Guid> TagIds { get; init; } = [];
    public bool? Billable { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public IReadOnlyList<ReportGroupBy> GroupBy { get; init; } = [];
}

public enum ReportGroupBy
{
    User,
    Project,
    Client,
    Task,
    Tag,
    Billable,
    Day,
    Week
}
