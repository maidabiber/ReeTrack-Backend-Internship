using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;

namespace ReeTrack.Api.Mapping;

/// <summary>
/// Shared mapping from API report-filter contracts to the application <see cref="ReportQuery"/>.
/// Used by reports and invoice generation (same filter shape).
/// </summary>
internal static class ReportQueryMapping
{
    public static ReportQuery FromRequest(ReportQueryRequest request) =>
        new()
        {
            UserIds = request.UserIds ?? [],
            ProjectIds = request.ProjectIds ?? [],
            ClientIds = request.ClientIds ?? [],
            TaskIds = request.TaskIds ?? [],
            TagIds = request.TagIds ?? [],
            Billable = request.Billable,
            From = request.From,
            To = request.To,
            GroupBy = (request.GroupBy ?? []).Select(ParseGroupBy).ToList()
        };

    private static ReportGroupBy ParseGroupBy(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "user" => ReportGroupBy.User,
            "project" => ReportGroupBy.Project,
            "client" => ReportGroupBy.Client,
            "task" => ReportGroupBy.Task,
            "tag" => ReportGroupBy.Tag,
            "billable" => ReportGroupBy.Billable,
            "day" => ReportGroupBy.Day,
            "week" => ReportGroupBy.Week,
            _ => throw AppErrors.Validation(
                "GroupBy must contain only: user, project, client, task, tag, billable, day, or week.")
        };
}
