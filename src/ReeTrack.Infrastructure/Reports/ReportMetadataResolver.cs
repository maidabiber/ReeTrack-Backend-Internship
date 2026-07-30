using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>
/// Small per-entry / per-report field resolutions shared across the report builders,
/// so each one doesn't re-derive its own fallback chain.
/// </summary>
internal static class ReportMetadataResolver
{
    /// <summary>
    /// Who ran the report, for export provenance. Never throws — an unresolvable user
    /// degrades the footer line, it must not fail the report.
    /// </summary>
    public static async Task<string?> ResolveGeneratedByAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return null;

        var userId = currentUser.UserId;
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
    }

    public static DateTime ResolveEntryInstant(TimeEntry entry) =>
        entry.StartedAtUtc ?? entry.CreatedAtUtc;

    public static DateOnly ResolveEntryDate(TimeEntry entry) =>
        DateOnly.FromDateTime(ResolveEntryInstant(entry));

    public static Guid? ResolveClientId(TimeEntry entry) =>
        entry.ClientId
        ?? entry.Project?.ClientId
        ?? entry.Client?.Id
        ?? entry.Project?.Client?.Id;

    public static string? ResolveClientName(TimeEntry entry) =>
        entry.Client?.Name
        ?? entry.Project?.Client?.Name;
}
