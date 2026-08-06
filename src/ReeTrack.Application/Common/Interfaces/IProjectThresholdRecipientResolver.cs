namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectThresholdRecipientResolver
{
    /// <summary>
    /// Resolves users who should receive threshold alerts for a project:
    /// all active Admins, plus the project's creator when active.
    /// </summary>
    Task<IReadOnlyList<ProjectThresholdRecipient>> GetRecipientsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectThresholdRecipient
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
}
