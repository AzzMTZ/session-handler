using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace SessionHandler.Exceptions;

/// <summary>
/// Maps the session domain exceptions to HTTP status codes and writes an RFC 7807
/// <c>ProblemDetails</c> body. Anything it does not recognise is left for the next
/// handler (ultimately a 500).
/// </summary>
public class SessionExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            SessionNotFoundException => StatusCodes.Status404NotFound,
            SessionAlreadyExistsException => StatusCodes.Status409Conflict,
            _ => (int?)null,
        };

        if (statusCode is not { } status)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = exception.Message,
            },
        });

        return true;
    }
}
