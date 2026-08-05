namespace ReeTrack.Application.Common.Interfaces;

public interface IProjectThresholdRecipientResolver
{
    /// <summary>
    /// Resolves users who should receive project threshold alerts (Admins today;
    /// Project Managers can be added when that role exists).
    /// </summary>
    Task<IReadOnlyList<ProjectThresholdRecipient>> GetRecipientsAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ProjectThresholdRecipient
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
}
