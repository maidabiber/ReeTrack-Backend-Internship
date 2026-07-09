using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Teammates;

public class TeammateService : ITeammateService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TeammateService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TeammateDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new AppException("Authentication is required.", 401);

        var teammates = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id != userId && u.Status == UserStatus.Active)
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .Select(u => new TeammateDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName
            })
            .ToListAsync(cancellationToken);

        return teammates;
    }
}
