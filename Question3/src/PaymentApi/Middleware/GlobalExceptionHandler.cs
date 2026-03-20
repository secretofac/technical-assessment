using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentApi.Services;

namespace PaymentApi.Middleware;

/// <summary>
/// Converts all unhandled exceptions into RFC 7807 ProblemDetails responses.
/// Never exposes stack traces or internal details to the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            IdempotencyConflictException ex => (
                StatusCodes.Status409Conflict,
                "Idempotency Key Conflict",
                ex.Message),

            IdempotencyInProgressException => (
                StatusCodes.Status409Conflict,
                "Request In Progress",
                "A request with this idempotency key is currently being processed. Please retry shortly."),

            OperationCanceledException => (
                StatusCodes.Status499ClientClosedRequest,
                "Request Cancelled",
                "The request was cancelled by the client."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
        };

        // Log the full exception server-side; never send it to the client.
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
