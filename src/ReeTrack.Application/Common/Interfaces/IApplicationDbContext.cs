using Microsoft.EntityFrameworkCore;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Invitation> Invitations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
