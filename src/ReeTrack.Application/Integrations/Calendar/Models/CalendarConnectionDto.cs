using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Integrations.Calendar.Models;

public class CalendarConnectionDto
{
    public Guid Id { get; init; }
    public CalendarProviderType ProviderType { get; init; }
    public string? ProviderAccountId { get; init; }
    public DateTime? LastSyncedAtUtc { get; init; }
    public CalendarSyncStatus SyncStatus { get; init; }
    public string? LastSyncError { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
