using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Reports;

public static class ReportQueryRules
{
    private const int MaxValuesPerDimension = 200;

    public static ReportQuery NormalizeAndValidate(ReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is { } from && query.To is { } to && from > to)
            throw new AppException("The report start date must be on or before the end date.", 400);

        if (query.GroupBy.Any(group => !Enum.IsDefined(group)))
            throw new AppException("The report contains an unsupported grouping.", 400);

        return new ReportQuery
        {
            UserIds = NormalizeIds(query.UserIds, "users"),
            ProjectIds = NormalizeIds(query.ProjectIds, "projects"),
            ClientIds = NormalizeIds(query.ClientIds, "clients"),
            TaskIds = NormalizeIds(query.TaskIds, "tasks"),
            TagIds = NormalizeIds(query.TagIds, "tags"),
            Billable = query.Billable,
            From = query.From,
            To = query.To,
            GroupBy = query.GroupBy.Distinct().ToList()
        };
    }

    private static IReadOnlyList<Guid> NormalizeIds(IEnumerable<Guid> ids, string dimension)
    {
        var normalized = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalized.Count > MaxValuesPerDimension)
        {
            throw new AppException(
                $"A report can filter by at most {MaxValuesPerDimension} {dimension}.",
                400);
        }

        return normalized;
    }
}
