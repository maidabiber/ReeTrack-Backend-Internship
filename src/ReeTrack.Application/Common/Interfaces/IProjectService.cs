using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(string? status, Guid? clientId, CancellationToken cancellationToken = default);

    Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
