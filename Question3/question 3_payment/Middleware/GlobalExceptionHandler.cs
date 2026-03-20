using Microsoft.AspNetCore.Diagnostics;
using PaymentApi.Services;

namespace PaymentApi.Middleware;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 ProblemDetails.
/// Never exposes stack traces or internal details.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // TODO: Map exception types to ProblemDetails responses:
        //   - IdempotencyConflictException     → 409
        //   - IdempotencyInProgressException   → 409
        //   - OperationCanceledException       → 499
        //   - Everything else                  → 500
        //
        // Log full exception server-side for 500s.
        // Never include exception details in the response body.
        throw new NotImplementedException();
    }
}
