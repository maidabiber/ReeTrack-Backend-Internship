namespace ReeTrack.Api.Contracts;

public sealed class CreateInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public short RoleId { get; set; }
}

public sealed class BatchInvitationRequest
{
    public List<string>? Emails { get; set; }
    public short RoleId { get; set; }
}

public sealed class BatchInvitationResponse
{
    public required IReadOnlyList<BatchInvitationRowResponse> Results { get; init; }
}

public sealed class BatchInvitationRowResponse
{
    public required string Email { get; init; }
    public required string Status { get; init; }
    public string? Message { get; init; }
    public MemberResponse? Member { get; init; }
}

public sealed class InvitationListItemResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required string InvitedByName { get; init; }
    public DateTime? AcceptedAtUtc { get; init; }
}

public sealed class RevokeInvitationResponse
{
    public required InvitationResponse Invitation { get; init; }
    public Guid? RemovedUserId { get; init; }
}

public sealed class CreateInvitationResponse
{
    public required MemberResponse Member { get; init; }
    public required InvitationResponse Invitation { get; init; }
}

public sealed class InvitationResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required short RoleId { get; init; }
    public required string Status { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required Guid InvitedByUserId { get; init; }
}

public sealed class AllowedDomainsResponse
{
    public required IReadOnlyList<string> Domains { get; init; }
}

public sealed class InvitationPreviewResponse
{
    public required string InvitedEmail { get; init; }
    public required string InviterName { get; init; }
    public required string Role { get; init; }
    public required string AppName { get; init; }
}
