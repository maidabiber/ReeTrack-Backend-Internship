namespace ReeTrack.Application.Common.Options;

public sealed class ReportOptions
{
    public const string SectionName = "Reports";

    /// <summary>
    /// Hard ceiling on confirmed entries a single report load may materialise.
    /// Above this the pipeline fails loudly rather than truncating.
    /// </summary>
    public int MaxEntriesPerReport { get; set; } = 250_000;
}
