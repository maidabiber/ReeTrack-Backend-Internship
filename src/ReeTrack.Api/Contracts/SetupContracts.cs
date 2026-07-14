namespace ReeTrack.Api.Contracts;

public sealed class SetupStatusResponse
{
    public required bool IsFirstRun { get; init; }
    public required bool RequiresAdminLogin { get; init; }
}
