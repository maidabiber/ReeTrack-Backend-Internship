using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken cancellationToken = default);
}
