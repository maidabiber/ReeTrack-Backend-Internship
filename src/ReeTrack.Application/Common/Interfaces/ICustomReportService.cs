using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Application.Common.Interfaces;

public interface ICustomReportService
{
    CustomReportCatalogueDto GetCatalogue();

    Task<CustomReportDto> RunAsync(
        CustomReportSpec spec,
        CancellationToken cancellationToken = default);

    /// <summary>Reuses a recent identical run when one exists. For exports and insights —
    /// operations derived from a report the caller has already seen.</summary>
    Task<CustomReportDto> GetOrRunAsync(
        CustomReportSpec spec,
        CancellationToken cancellationToken = default);

    Task<ReportFile> ExportAsync(
        CustomReportSpec spec,
        ReportExportFormat format,
        CancellationToken cancellationToken = default);
}
