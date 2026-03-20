using PaymentApi.Data;

namespace PaymentApi.Services;

public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to claim the idempotency key (INSERT with status "Processing").
    /// Returns null if claimed successfully (first request).
    /// Returns existing record if key already exists (retry).
    /// </summary>
    Task<IdempotencyRecord?> TryClaimKeyAsync(
        string idempotencyKey,
        string merchantId,
        string requestHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a claimed key as completed and stores the cached response.
    /// </summary>
    Task CompleteAsync(
        string idempotencyKey,
        string merchantId,
        string status,
        string responseBody,
        int httpStatusCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Releases a key stuck in "Processing" (e.g., after a gateway failure)
    /// so the client can retry.
    /// </summary>
    Task ReleaseAsync(
        string idempotencyKey,
        string merchantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes records older than the configured retention period.
    /// </summary>
    Task CleanupExpiredAsync(CancellationToken cancellationToken);
}
