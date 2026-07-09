using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectTaskService
{
    Task<IReadOnlyList<ProjectTaskDto>> ListAsync(Guid projectId, string? status, CancellationToken cancellationToken = default);

    Task<ProjectTaskDto> CreateAsync(Guid projectId, CreateTaskInput input, CancellationToken cancellationToken = default);

    Task<ProjectTaskDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
}
