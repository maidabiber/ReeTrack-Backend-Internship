using ReeTrack.Application.Common.Exceptions;

namespace ReeTrack.Application.Integrations.Calendar;

public sealed class CalendarIntegrationException : Exception
{
    public int StatusCode { get; }

    public ErrorCode Code { get; }

    public CalendarIntegrationException(string message, int statusCode = 400, ErrorCode code = ErrorCode.Unspecified)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
