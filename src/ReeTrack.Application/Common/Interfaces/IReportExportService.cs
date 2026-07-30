using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

/// <summary>
/// One export writer for one report format (CSV/Excel/PDF), for one report model type.
/// Was 4 near-identical interfaces (one per report type) differing only in the model
/// type passed to <c>Write</c> — collapsed once generic, since every implementation
/// already had the exact same shape.
/// </summary>
public interface IReportWriter<TModel>
{
    ReportExportFormat Format { get; }

    ReportFile Write(TModel model);
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

    Task<ReportFile> ExportWorkloadAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ReportFile> ExportProfitabilityAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default);
}
