using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class UserCalendarConnection : BaseEntity
{
    public Guid UserId { get; set; }
    public CalendarProviderType ProviderType { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpirationDateTime { get; set; }
    public string? ProviderAccountId { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public CalendarSyncStatus SyncStatus { get; set; }
    public string? LastSyncError { get; set; }

    public User User { get; set; } = null!;
    public ICollection<SyncedCalendarEvent> SyncedEvents { get; set; } = [];
}
