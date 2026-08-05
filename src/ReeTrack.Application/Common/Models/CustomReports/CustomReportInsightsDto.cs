namespace ReeTrack.Application.Common.Models.CustomReports;

/// <summary>
/// Generated commentary for one narrative block. The caller writes these back onto the block's
/// spec so the text survives without another model call.
/// </summary>
public sealed class CustomReportInsightsDto
{
    public required string BlockId { get; init; }

    /// <summary>One paragraph per finding, already carrying figures read out of the report.</summary>
    public required IReadOnlyList<string> Paragraphs { get; init; }

    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// Identifies the report shape these paragraphs describe. Stored on the block so the
    /// renderer can tell the reader when the text no longer matches the data.
    /// </summary>
    public required string Fingerprint { get; init; }
}
