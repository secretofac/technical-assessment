using PaymentApi.Configuration;

namespace PaymentApi.Middleware;

/// <summary>
/// Sliding window rate limiter partitioned per merchant.
///
/// Sliding window avoids the fixed-window "boundary burst" problem where
/// a merchant can double throughput at window boundaries.
///
/// Merchant identity is resolved from authenticated JWT claims first,
/// falling back to X-Merchant-Id header for development only.
/// </summary>
public static class MerchantRateLimitPolicy
{
    public const string PolicyName = "PerMerchant";

    public static IServiceCollection AddMerchantRateLimiting(
        this IServiceCollection services)
    {
        // TODO: Implementation steps:
        //   1. services.AddRateLimiter(options => { ... })
        //   2. Set RejectionStatusCode = 429
        //   3. OnRejected: write RFC 7807 ProblemDetails + Retry-After header
        //   4. AddPolicy with SlidingWindowRateLimiter partitioned by merchant ID
        //   5. Use MerchantOverrides from RateLimitOptions for per-merchant limits
        //   6. QueueLimit = 0 (reject immediately, no backpressure queueing)
        throw new NotImplementedException();
    }
}
