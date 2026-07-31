namespace ReeTrack.Api.Contracts;

public sealed class UpdateMemberRequest
{
    public short? RoleId { get; set; }
    /// <summary>"Active" or "Disabled".</summary>
    public string? Status { get; set; }
}

public sealed class MemberResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required bool EmailVerified { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public Guid? PendingInvitationId { get; init; }
    /// <summary>Override mode when set; null means app default.</summary>
    public string? HourTargetMode { get; init; }
    /// <summary>Override hours when set; null means app default.</summary>
    public decimal? HourTargetHours { get; init; }
}
