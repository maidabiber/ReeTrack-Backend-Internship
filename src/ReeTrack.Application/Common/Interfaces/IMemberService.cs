using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface IMemberService
{
    Task<PagedResult<MemberDto>> ListAsync(MemberListQuery query, CancellationToken cancellationToken = default);

    Task<MemberDto> UpdateAsync(
        Guid userId,
        short? roleId,
        UserStatus? status,
        CancellationToken cancellationToken = default);
}
