using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IReportFilterSetService
{
    Task<PagedResult<ReportFilterSetDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ReportFilterSetDto> CreateAsync(
        string? name,
        ReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ReportFilterSetDto> UpdateAsync(
        Guid id,
        string? name,
        ReportQuery query,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
