using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IInvitationService
{
    Task<CreateInvitationResult> CreateAsync(
        string email,
        short roleId,
        CancellationToken cancellationToken = default);

    Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<InvitationPreviewDto> GetPreviewAsync(string token, CancellationToken cancellationToken = default);
}
