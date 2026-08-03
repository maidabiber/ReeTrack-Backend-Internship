using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default);

    Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateWithTasksAsync(CreateProjectWithTasksInput input, CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectLookupDto>> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);
}
