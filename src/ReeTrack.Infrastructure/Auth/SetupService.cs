using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;

namespace ReeTrack.Infrastructure.Auth;

public class SetupService : ISetupService
{
    private readonly IApplicationDbContext _db;

    public SetupService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasUsers = await _db.Users.AnyAsync(cancellationToken);

        return new SetupStatus
        {
            IsFirstRun = !hasUsers,
            RequiresAdminLogin = !hasUsers
        };
    }
}
