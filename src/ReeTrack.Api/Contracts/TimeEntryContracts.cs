namespace ReeTrack.Api.Contracts;

public abstract class TimeEntryFieldsRequest
{
    public string? Description { get; set; }
    public bool? IsBillable { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public List<Guid>? TagIds { get; set; }
}

public abstract class TimeEntryRangeRequest : TimeEntryFieldsRequest
{
    public required DateTime StartedAtUtc { get; set; }
    public required DateTime EndedAtUtc { get; set; }
}

public abstract class TimeEntryDurationRequest : TimeEntryFieldsRequest
{
    public required DateTime EntryDateUtc { get; set; }
    public required int DurationSeconds { get; set; }
}

public abstract class SharedAssigneeTimeEntryRangeRequest : TimeEntryRangeRequest
{
    public Guid? AssigneeUserId { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
}

public abstract class SharedAssigneeTimeEntryDurationRequest : TimeEntryDurationRequest
{
    public Guid? AssigneeUserId { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
}

public sealed class StartTimerRequest : TimeEntryFieldsRequest;

public sealed class StopTimerRequest
{
    public string? Description { get; set; }
    public List<Guid>? AssigneeUserIds { get; set; }
    public bool? IsBillable { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectTaskId { get; set; }
    public List<Guid>? TagIds { get; set; }
}

public sealed class CreateManualEntryRequest : TimeEntryRangeRequest;

public sealed class CreateDurationOnlyEntryRequest : TimeEntryDurationRequest;

public sealed class CreateSharedManualEntryRequest : SharedAssigneeTimeEntryRangeRequest;

public sealed class CreateSharedDurationOnlyEntryRequest : SharedAssigneeTimeEntryDurationRequest;

public sealed class ShareExistingEntryRequest
{
    public List<Guid>? AssigneeUserIds { get; set; }
}

public sealed class CreateSharedManualEntryResponse
{
    public required IReadOnlyList<TimeEntryResponse> Entries { get; init; }
}

public sealed class CreateManualEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
}

public sealed class UpdateTimeEntryRequest : TimeEntryRangeRequest;

public sealed class UpdateDurationOnlyEntryRequest : TimeEntryDurationRequest;

public sealed class UpdateTimeEntryResponse
{
    public required TimeEntryResponse Entry { get; init; }
}

public sealed class TimeEntryTagResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Color { get; init; }
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
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectColor { get; init; }
    public Guid? ProjectTaskId { get; init; }
    public string? ProjectTaskName { get; init; }
    public IReadOnlyList<TimeEntryTagResponse> Tags { get; init; } = [];
}

public sealed class TimeEntryParticipantResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}
