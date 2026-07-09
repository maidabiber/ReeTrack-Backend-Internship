namespace ReeTrack.Application.Integrations.Calendar;

public sealed class CalendarIntegrationException : Exception
{
    public int StatusCode { get; }

    public CalendarIntegrationException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
