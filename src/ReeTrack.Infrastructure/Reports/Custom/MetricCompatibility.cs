using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom;

internal static class MetricCompatibility
{
    private static readonly HashSet<string> ProjectCompatibleDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "project",
        "client"
    };

    private static readonly HashSet<string> UserCompatibleDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "user"
    };

    public static bool IsValid(MetricDefinition metric, IReadOnlyList<string> dimensions)
    {
        if (dimensions.Count == 0)
            return true; // KPI / ungrouped

        return metric.Scope switch
        {
            MetricScope.Entry => true,
            MetricScope.Project => dimensions.All(ProjectCompatibleDimensions.Contains),
            MetricScope.User => dimensions.All(UserCompatibleDimensions.Contains),
            _ => false
        };
    }

    public static IReadOnlyList<string> CompatibleDimensions(MetricDefinition metric) =>
        metric.Scope switch
        {
            MetricScope.Entry => DimensionCatalog.All.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            MetricScope.Project => ProjectCompatibleDimensions.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            MetricScope.User => UserCompatibleDimensions.OrderBy(k => k, StringComparer.Ordinal).ToList(),
            _ => []
        };
}
