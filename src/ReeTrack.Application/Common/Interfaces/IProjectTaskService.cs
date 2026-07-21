using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectTaskService
{
    Task<PagedResult<ProjectTaskDto>> ListAsync(TaskListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists open tasks across all projects, optionally filtered by name (or project name).
    /// </summary>
    Task<PagedResult<ProjectTaskDto>> ListOpenAsync(TaskListQuery query, CancellationToken cancellationToken = default);

    Task<ProjectTaskDto> CreateAsync(Guid projectId, CreateTaskInput input, CancellationToken cancellationToken = default);

    Task<ProjectTaskDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
}
