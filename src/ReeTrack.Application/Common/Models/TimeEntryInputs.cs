namespace ReeTrack.Application.Common.Models;

public sealed class StartTimerInput
{
    public string? Description { get; init; }
    public bool IsBillable { get; init; } = true;
}

public sealed class StopTimerInput
{
    public string? Description { get; init; }
}

public sealed class CreateManualEntryInput
{
    public string? Description { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public bool IsBillable { get; init; } = true;
    public bool ConfirmOverlap { get; init; }
}

public sealed class CreateDurationOnlyEntryInput
{
    public string? Description { get; init; }
    public DateTime EntryDateUtc { get; init; }
    public int DurationSeconds { get; init; }
    public bool IsBillable { get; init; } = true;
}

public sealed class UpdateTimeEntryInput
{
    public string? Description { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public bool IsBillable { get; init; }
    public bool ConfirmOverlap { get; init; }
}

public sealed class UpdateDurationOnlyEntryInput
{
    public string? Description { get; init; }
    public DateTime EntryDateUtc { get; init; }
    public int DurationSeconds { get; init; }
    public bool IsBillable { get; init; }
}

public sealed class StopSharedTimerInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
    public string? Description { get; init; }
    public bool ConfirmOverlap { get; init; }
}

public sealed class CreateSharedManualEntryInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
    public string? Description { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public bool IsBillable { get; init; } = true;
    public bool ConfirmOverlap { get; init; }
}

public sealed class CreateSharedDurationOnlyEntryInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
    public string? Description { get; init; }
    public DateTime EntryDateUtc { get; init; }
    public int DurationSeconds { get; init; }
    public bool IsBillable { get; init; } = true;
}

public sealed class ShareExistingEntryInput
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; init; } = [];
    public bool ConfirmOverlap { get; init; }
}

public sealed class UpdatePendingEntryInput
{
    public string? Description { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public bool IsBillable { get; init; }
    public bool ConfirmOverlap { get; init; }
}
