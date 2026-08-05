using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IOverviewService
{
    Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken = default);
}
