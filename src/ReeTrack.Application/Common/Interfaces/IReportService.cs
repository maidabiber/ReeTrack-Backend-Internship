using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IReportService
{
    Task<SummaryReportDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
