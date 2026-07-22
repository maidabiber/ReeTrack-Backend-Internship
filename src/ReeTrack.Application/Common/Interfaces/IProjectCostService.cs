using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectCostService
{
    Task<ProjectCostDto> CalculateAsync(Guid projectId, CancellationToken cancellationToken = default);
}
