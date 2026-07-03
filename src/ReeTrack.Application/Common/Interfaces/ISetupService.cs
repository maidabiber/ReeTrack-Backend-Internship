namespace ReeTrack.Application.Common.Interfaces;

public interface ISetupService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class SetupStatus
{
    public required bool IsFirstRun { get; init; }
    public required bool RequiresAdminLogin { get; init; }
}
