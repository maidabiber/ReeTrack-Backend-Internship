namespace ReeTrack.Api.Contracts;

public sealed class CreateTaskRequest
{
    public string? Name { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public decimal? TimeEstimateHours { get; set; }
}

public sealed class UpdateTaskRequest
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public decimal? TimeEstimateHours { get; set; }
}

public sealed class TaskResponse
{
    public required Guid Id { get; init; }
    public required Guid ProjectId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required Guid? AssignedToUserId { get; init; }
    public required string? AssignedToName { get; init; }
    public required decimal? TimeEstimateHours { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
