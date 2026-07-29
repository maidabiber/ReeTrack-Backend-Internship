using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportExportService : IReportExportService
{
    private readonly IReportService _reports;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter> _writers;

    public ReportExportService(IReportService reports, IEnumerable<IReportWriter> writers)
    {
        _reports = reports;
        _writers = writers.ToDictionary(w => w.Format);
    }

    public async Task<ReportFile> ExportSummaryAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_writers.TryGetValue(format, out var writer))
            throw new AppException($"Unsupported export format '{format}'.", 400);

        var summary = await _reports.GetSummaryAsync(query, cancellationToken);
        return writer.Write(summary);
    }
}
