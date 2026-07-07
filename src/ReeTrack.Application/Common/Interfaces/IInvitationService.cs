using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IInvitationService
{
    Task<CreateInvitationResult> CreateAsync(
        string email,
        short roleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchInvitationRowResult>> CreateManyAsync(
        IReadOnlyList<string> emails,
        short roleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvitationListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<InvitationDto> ResendAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<RevokeInvitationResult> RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<InvitationPreviewDto> GetPreviewAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email domains allowed to be invited (mirrors the SSO domain). Empty means
    /// any domain is allowed; the SPA uses this to warn before submitting.
    /// </summary>
    IReadOnlyList<string> GetAllowedDomains();
}
