using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITeammateService
{
    Task<IReadOnlyList<TeammateDto>> ListAsync(CancellationToken cancellationToken = default);
}
