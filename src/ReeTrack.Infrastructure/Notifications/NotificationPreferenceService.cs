using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Notifications;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Notifications;

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly IApplicationDbContext _db;

    public NotificationPreferenceService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var preferences = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.NotificationType)
            .ThenBy(p => p.DeliveryChannel)
            .ToListAsync(cancellationToken);

        return preferences.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> UpsertAsync(
        Guid userId,
        IReadOnlyList<UpsertNotificationPreferenceDto> preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await EnsureUserExistsAsync(userId, cancellationToken);

        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var incoming in preferences)
        {
            var match = existing.FirstOrDefault(p =>
                p.NotificationType == incoming.NotificationType
                && p.DeliveryChannel == incoming.DeliveryChannel);

            if (match is null)
            {
                _db.NotificationPreferences.Add(new NotificationPreference
                {
                    UserId = userId,
                    NotificationType = incoming.NotificationType,
                    DeliveryChannel = incoming.DeliveryChannel,
                    IsEnabled = incoming.IsEnabled
                });
            }
            else
            {
                match.IsEnabled = incoming.IsEnabled;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetByUserAsync(userId, cancellationToken);
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            throw new AppException("User was not found.", 404);
    }

    private static NotificationPreferenceDto Map(NotificationPreference preference) =>
        new()
        {
            Id = preference.Id,
            UserId = preference.UserId,
            NotificationType = preference.NotificationType,
            DeliveryChannel = preference.DeliveryChannel,
            IsEnabled = preference.IsEnabled
        };
}
