namespace ReeTrack.Api.Contracts;

public sealed class StartTimerRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
}

public sealed class StopTimerRequest
{
    public string? Description { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateManualEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateDurationOnlyEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public sealed class CreateSharedManualEntryRequest
{
    public Guid? AssigneeUserId { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateSharedDurationOnlyEntryRequest
{
    public Guid? AssigneeUserId { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public sealed class ShareExistingEntryRequest
{
    public List<Guid>? AssigneeUserIds { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class CreateSharedManualEntryResponse
{
    public required IReadOnlyList<TimeEntryResponse> Entries { get; init; }
    public string? OverlapWarning { get; init; }
}

public sealed class CreateManualEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
    public string? OverlapWarning { get; init; }
}

public sealed class UpdateTimeEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
    public bool ConfirmOverlap { get; set; }
}

public sealed class UpdateDurationOnlyEntryRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public sealed class UpdateTimeEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
    public string? OverlapWarning { get; init; }
}

public sealed class TimeEntryResponse
{
    public required Guid Id { get; init; }
    public string? Description { get; init; }
    public required bool IsBillable { get; init; }
    public required string Mode { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }
    public required int DurationSeconds { get; init; }
    public required bool IsRunning { get; init; }
    public required string Status { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByDisplayName { get; init; }
    public Guid? AssigneeUserId { get; init; }
    public string? AssigneeDisplayName { get; init; }
    public Guid? ShareGroupId { get; init; }
    public IReadOnlyList<TimeEntryParticipantResponse> Participants { get; init; } = [];
}

public sealed class TimeEntryParticipantResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}
