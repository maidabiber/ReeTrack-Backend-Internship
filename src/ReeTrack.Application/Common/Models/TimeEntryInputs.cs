namespace ReeTrack.Application.Common.Models;

public abstract class TimeEntryFieldsInput
{
    public string? Description { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectTaskId { get; init; }
    public IReadOnlyList<Guid>? TagIds { get; init; }
}

public abstract class TimeEntryCreateFieldsInput : TimeEntryFieldsInput
{
    public bool IsBillable { get; init; } = true;
}

public abstract class TimeEntryUpdateFieldsInput : TimeEntryFieldsInput
{
    public bool IsBillable { get; init; }
}

public abstract class TimeEntryRangeInput : TimeEntryCreateFieldsInput
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
}

public abstract class TimeEntryUpdateRangeInput : TimeEntryUpdateFieldsInput
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
}

public abstract class TimeEntryDurationInput : TimeEntryCreateFieldsInput
{
    public DateTime EntryDateUtc { get; init; }
    public int DurationSeconds { get; init; }
}

public abstract class TimeEntryUpdateDurationInput : TimeEntryUpdateFieldsInput
{
    public DateTime EntryDateUtc { get; init; }
    public int DurationSeconds { get; init; }
}

public abstract class SharedAssigneeTimeEntryRangeInput : TimeEntryRangeInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
}

public abstract class SharedAssigneeTimeEntryDurationInput : TimeEntryDurationInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
}

public sealed class StartTimerInput : TimeEntryCreateFieldsInput;

public sealed class StopTimerInput : TimeEntryFieldsInput
{
    public bool? IsBillable { get; init; }
}

public sealed class CreateManualEntryInput : TimeEntryRangeInput;

public sealed class CreateDurationOnlyEntryInput : TimeEntryDurationInput;

public sealed class UpdateTimeEntryInput : TimeEntryUpdateRangeInput;

public sealed class UpdateDurationOnlyEntryInput : TimeEntryUpdateDurationInput;

public sealed class StopSharedTimerInput : TimeEntryFieldsInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
    public bool? IsBillable { get; init; }
}

public sealed class CreateSharedManualEntryInput : SharedAssigneeTimeEntryRangeInput;

public sealed class CreateSharedDurationOnlyEntryInput : SharedAssigneeTimeEntryDurationInput;

public sealed class ShareExistingEntryInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
}

public sealed class UpdatePendingEntryInput : TimeEntryUpdateRangeInput;
