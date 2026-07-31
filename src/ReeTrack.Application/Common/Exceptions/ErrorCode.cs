using System.Text.Json.Serialization;

namespace ReeTrack.Application.Common.Exceptions;

/// <summary>
/// Machine-readable error identifiers carried alongside <see cref="AppException.Message"/>.
/// Serialized as its string name so the frontend can branch on `error.code` instead of
/// pattern-matching the human-readable message text.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ErrorCode
{
    Unspecified = 0,
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden,
    RoleInvalid,
    StatusInvalid,
    TeammatesRequired,
    ExportFormatInvalid,
    DurationLimitExceeded,
    EntryOverlap,
    AlreadyRunning,
    ServiceUnavailable,
}
