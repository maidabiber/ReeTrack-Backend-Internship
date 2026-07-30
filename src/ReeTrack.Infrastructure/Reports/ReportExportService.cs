using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportExportService : IReportExportService
{
    private readonly IReportService _reports;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter> _summaryWriters;
    private readonly IReadOnlyDictionary<ReportExportFormat, IDetailedReportWriter> _detailedWriters;

    public ReportExportService(
        IReportService reports,
        IEnumerable<IReportWriter> summaryWriters,
        IEnumerable<IDetailedReportWriter> detailedWriters)
    {
        _reports = reports;
        _summaryWriters = summaryWriters.ToDictionary(w => w.Format);
        _detailedWriters = detailedWriters.ToDictionary(w => w.Format);
    }

    public async Task<ReportFile> ExportSummaryAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_summaryWriters.TryGetValue(format, out var writer))
            throw new AppException($"Unsupported export format '{format}'.", 400);

        var summary = await _reports.GetSummaryAsync(query, cancellationToken);
        return writer.Write(summary);
    }

    public async Task<ReportFile> ExportDetailedAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_detailedWriters.TryGetValue(format, out var writer))
            throw new AppException($"Unsupported export format '{format}'.", 400);

        // pageSize <= 0 returns every filtered row for the audit export.
        var detailed = await _reports.GetDetailedAsync(query, page: 1, pageSize: 0, cancellationToken);
        return writer.Write(detailed);
    }
}
