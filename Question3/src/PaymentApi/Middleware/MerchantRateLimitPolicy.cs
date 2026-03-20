using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PaymentApi.Configuration;

namespace PaymentApi.Middleware;

/// <summary>
/// Configures a sliding window rate limiter partitioned per merchant.
///
/// Why sliding window instead of fixed window:
/// A fixed-window counter has the "boundary burst" problem — a merchant can
/// send N requests at the end of one window and N more at the start of the
/// next, effectively doubling throughput at the boundary. A sliding window
/// divides the window into segments and tracks usage across them, smoothing
/// out bursts and providing more accurate rate limiting.
///
/// Why per-merchant:
/// Rate limiting must be scoped to the merchant identity (from authenticated
/// claims) so that one abusive merchant cannot exhaust shared resources while
/// legitimate merchants continue operating normally.
/// </summary>
public static class MerchantRateLimitPolicy
{
    public const string PolicyName = "PerMerchant";

    public static IServiceCollection AddMerchantRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<RateLimiterOptions>>();

                var merchantId = ResolveMerchantId(context.HttpContext);

                logger.LogWarning(
                    "Rate limit exceeded for merchant {MerchantId} on {Path}",
                    merchantId, context.HttpContext.Request.Path);

                context.HttpContext.Response.ContentType = "application/problem+json";

                var retryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue
                    : TimeSpan.FromSeconds(60);

                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString();

                var problemDetails = new
                {
                    type = "https://httpstatuses.com/429",
                    title = "Too Many Requests",
                    status = 429,
                    detail = $"Rate limit exceeded. Retry after {(int)retryAfter.TotalSeconds} seconds.",
                    instance = context.HttpContext.Request.Path.Value
                };

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problemDetails, cancellationToken);
            };

            options.AddPolicy(PolicyName, httpContext =>
            {
                var merchantId = ResolveMerchantId(httpContext);
                var rateLimitOptions = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                var permitLimit = rateLimitOptions.MerchantOverrides
                    .TryGetValue(merchantId, out var merchantLimit)
                    ? merchantLimit
                    : rateLimitOptions.DefaultPermitLimit;

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: merchantId,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSizeSeconds),
                        SegmentsPerWindow = rateLimitOptions.SegmentsPerWindow,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0 // Reject immediately — no queueing to avoid backpressure buildup
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// Resolves the merchant identity from authenticated claims first,
    /// falling back to a header for development only. In production,
    /// the claim should always be present from the auth middleware.
    /// </summary>
    private static string ResolveMerchantId(HttpContext httpContext)
    {
        // Prefer authenticated claim — cannot be spoofed.
        var merchantClaim = httpContext.User.FindFirstValue("merchant_id");
        if (!string.IsNullOrEmpty(merchantClaim))
        {
            return merchantClaim;
        }

        // Fallback for development/testing — header can be spoofed in production.
        var headerValue = httpContext.Request.Headers["X-Merchant-Id"].FirstOrDefault();
        return !string.IsNullOrEmpty(headerValue) ? headerValue : "unknown";
    }
}
