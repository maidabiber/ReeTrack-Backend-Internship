namespace ReeTrack.Application.Common.Models;

public sealed class TimeEntryParticipantDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}
