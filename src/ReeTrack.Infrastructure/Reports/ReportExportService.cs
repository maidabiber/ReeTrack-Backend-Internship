using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportExportService : IReportExportService
{
    private readonly IReportService _reports;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter<SummaryReportDto>> _summaryWriters;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter<DetailedReportDto>> _detailedWriters;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter<WorkloadReportDto>> _workloadWriters;
    private readonly IReadOnlyDictionary<ReportExportFormat, IReportWriter<ProfitabilityReportDto>> _profitabilityWriters;

    public ReportExportService(
        IReportService reports,
        IEnumerable<IReportWriter<SummaryReportDto>> summaryWriters,
        IEnumerable<IReportWriter<DetailedReportDto>> detailedWriters,
        IEnumerable<IReportWriter<WorkloadReportDto>> workloadWriters,
        IEnumerable<IReportWriter<ProfitabilityReportDto>> profitabilityWriters)
    {
        _reports = reports;
        _summaryWriters = ToDictionary(summaryWriters);
        _detailedWriters = ToDictionary(detailedWriters);
        _workloadWriters = ToDictionary(workloadWriters);
        _profitabilityWriters = ToDictionary(profitabilityWriters);
    }

    public Task<ReportFile> ExportSummaryAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default) =>
        ExportAsync(_summaryWriters, format, () => _reports.GetSummaryAsync(query, cancellationToken));

    public Task<ReportFile> ExportDetailedAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default) =>
        ExportAsync(
            _detailedWriters,
            format,
            () => _reports.GetDetailedAsync(query, page: 1, pageSize: 0, cancellationToken));

    public Task<ReportFile> ExportWorkloadAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default) =>
        ExportAsync(_workloadWriters, format, () => _reports.GetWorkloadAsync(query, cancellationToken));

    public Task<ReportFile> ExportProfitabilityAsync(
        ReportExportFormat format,
        ReportQuery query,
        CancellationToken cancellationToken = default) =>
        ExportAsync(_profitabilityWriters, format, () => _reports.GetProfitabilityAsync(query, cancellationToken));

    /// <summary>
    /// The four Export*Async methods above were textually identical modulo which
    /// dictionary they looked in and which <see cref="IReportService"/> call loaded the
    /// model — <see cref="IReportService"/> has four differently-shaped methods (e.g.
    /// Detailed takes paging params), so this takes the load as a delegate rather than
    /// unifying <see cref="IReportService"/> itself.
    /// </summary>
    private static async Task<ReportFile> ExportAsync<TModel>(
        IReadOnlyDictionary<ReportExportFormat, IReportWriter<TModel>> writers,
        ReportExportFormat format,
        Func<Task<TModel>> loadModel)
    {
        if (!writers.TryGetValue(format, out var writer))
            throw new AppException($"Unsupported export format '{format}'.", 400);

        var model = await loadModel();
        return writer.Write(model);
    }

    private static IReadOnlyDictionary<ReportExportFormat, IReportWriter<TModel>> ToDictionary<TModel>(
        IEnumerable<IReportWriter<TModel>> writers) =>
        writers.ToDictionary(w => w.Format);
}
