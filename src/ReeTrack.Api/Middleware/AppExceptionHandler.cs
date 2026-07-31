using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Application.Common.Exceptions;

namespace ReeTrack.Api.Middleware;

/// <summary>
/// Maps <see cref="AppException"/> thrown anywhere in the pipeline to an RFC 7807
/// ProblemDetails body carrying both a machine-readable "code" and the human-readable
/// "message" the frontend already renders, so actions no longer need per-endpoint
/// try/catch blocks. Anything else is logged here since it otherwise falls through
/// to the framework's default handler unlogged.
/// </summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(ILogger<AppExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = appException.StatusCode,
            Detail = appException.Message,
        };
        problemDetails.Extensions["code"] = appException.Code.ToString();
        problemDetails.Extensions["message"] = appException.Message;

        httpContext.Response.StatusCode = appException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
