using Microsoft.EntityFrameworkCore;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Client> Clients { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<Tag> Tags { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<TimeEntryTag> TimeEntryTags { get; }
    DbSet<UserCalendarConnection> UserCalendarConnections { get; }
    DbSet<SyncedCalendarEvent> SyncedCalendarEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
