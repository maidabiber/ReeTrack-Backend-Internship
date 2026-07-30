using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IReportWriter
{
    ReportExportFormat Format { get; }

    ReportFile Write(SummaryReportDto model);
}

public interface IDetailedReportWriter
{
    ReportExportFormat Format { get; }

    ReportFile Write(DetailedReportDto model);
}

public interface IReportExportService
{
    Task<ReportFile> ExportSummaryAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ReportFile> ExportDetailedAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default);
}
