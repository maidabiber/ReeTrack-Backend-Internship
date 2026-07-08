using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IClientService
{
    Task<IReadOnlyList<ClientDto>> ListAsync(string? status, CancellationToken cancellationToken = default);

    Task<ClientDto> CreateAsync(string? name, CancellationToken cancellationToken = default);

    Task<ClientDto> UpdateAsync(Guid id, string? name, bool? isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
