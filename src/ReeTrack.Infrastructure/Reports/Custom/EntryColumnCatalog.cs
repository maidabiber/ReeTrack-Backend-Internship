namespace ReeTrack.Infrastructure.Reports.Custom;

internal static class EntryColumnCatalog
{
    public static IReadOnlyDictionary<string, string> All { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = "Date",
            ["user"] = "Member",
            ["client"] = "Client",
            ["project"] = "Project",
            ["task"] = "Task",
            ["tags"] = "Tags",
            ["billable"] = "Billable",
            ["hours"] = "Hours",
            ["labourCost"] = "Labour cost",
            ["currency"] = "Currency",
            ["description"] = "Description",
        };
}
