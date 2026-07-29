using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IClientService
{
    Task<PagedResult<ClientDto>> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default);

    Task<ClientDto> CreateAsync(string? name, CancellationToken cancellationToken = default);

    Task<ClientDto> UpdateAsync(Guid id, string? name, bool? isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
