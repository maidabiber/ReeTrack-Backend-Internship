using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Application.Common.Interfaces;

public interface ICustomReportInsightService
{
    /// <summary>
    /// Runs the spec, then asks the model to comment on the result. Deliberately separate from
    /// <see cref="ICustomReportService.RunAsync"/>: a run is fast, free, and reproducible, and
    /// folding a model call into it would make all three untrue.
    /// </summary>
    Task<CustomReportInsightsDto> GenerateAsync(
        CustomReportSpec spec,
        string blockId,
        CancellationToken cancellationToken = default);
}
