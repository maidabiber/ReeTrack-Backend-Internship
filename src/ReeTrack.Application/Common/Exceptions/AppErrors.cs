namespace ReeTrack.Application.Common.Exceptions;

/// <summary>
/// Factory methods for the most-duplicated <see cref="AppException"/> shapes, so call sites
/// stop hand-writing status codes and message wording that drift out of sync with each other.
/// </summary>
public static class AppErrors
{
    public static AppException NotFound(string resource) =>
        new($"{resource} was not found.", 404, ErrorCode.NotFound);

    public static AppException Conflict(string message) =>
        new(message, 409, ErrorCode.Conflict);

    public static AppException Validation(string message) =>
        new(message, 400, ErrorCode.Validation);

    public static AppException Unauthorized(string message) =>
        new(message, 401, ErrorCode.Unauthorized);

    public static AppException Forbidden(string message) =>
        new(message, 403, ErrorCode.Forbidden);
}
