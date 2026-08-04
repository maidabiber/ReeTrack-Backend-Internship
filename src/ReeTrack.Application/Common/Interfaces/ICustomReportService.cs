using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Application.Common.Interfaces;

public interface ICustomReportService
{
    CustomReportCatalogueDto GetCatalogue();

    Task<CustomReportDto> RunAsync(
        CustomReportSpec spec,
        CancellationToken cancellationToken = default);
}
