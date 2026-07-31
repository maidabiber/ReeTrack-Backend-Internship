namespace ReeTrack.Application.Common.Exceptions;

public sealed class AppException : Exception
{
    public int StatusCode { get; }

    public ErrorCode Code { get; }

    public AppException(string message, int statusCode = 400, ErrorCode code = ErrorCode.Unspecified)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
