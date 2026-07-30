using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IReportService
{
    Task<SummaryReportDto> GetSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Entry-level audit report. Pass <paramref name="pageSize"/> &lt;= 0 to return every
    /// filtered row (exports); otherwise results are paginated.
    /// </summary>
    Task<DetailedReportDto> GetDetailedAsync(
        ReportQuery query,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
