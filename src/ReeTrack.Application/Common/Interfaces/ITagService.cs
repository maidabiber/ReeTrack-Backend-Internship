using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITagService
{
    Task<PagedResult<TagDto>> ListAsync(TagListQuery query, CancellationToken cancellationToken = default);

    Task<TagDto> CreateAsync(string? name, string? color, CancellationToken cancellationToken = default);

    Task<TagDto> UpdateAsync(Guid id, string? name, string? color, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
