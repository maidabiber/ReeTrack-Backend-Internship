using Microsoft.AspNetCore.Diagnostics;
using ReeTrack.Application.Common.Exceptions;

namespace ReeTrack.Api.Middleware;

/// <summary>
/// Maps <see cref="AppException"/> thrown anywhere in the pipeline to the same
/// <c>{ "message": ... }</c> JSON body the controllers used to build by hand,
/// so actions no longer need per-endpoint try/catch blocks.
/// </summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
            return false;

        httpContext.Response.StatusCode = appException.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new { message = appException.Message },
            cancellationToken);

        return true;
    }
}
