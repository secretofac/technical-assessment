using PaymentApi.Data;

namespace PaymentApi.Services;

public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to claim the idempotency key by inserting a record with status "Processing".
    /// Returns null if the key was successfully claimed (first request).
    /// Returns the existing record if the key already exists (retry).
    /// </summary>
    Task<IdempotencyRecord?> TryClaimKeyAsync(
        string idempotencyKey,
        string merchantId,
        string requestHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a claimed key as completed (Succeeded or Failed) and stores the response.
    /// </summary>
    Task CompleteAsync(
        string idempotencyKey,
        string merchantId,
        string status,
        string responseBody,
        int httpStatusCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Releases a key that was claimed but never completed (e.g., gateway call failed
    /// with an exception). This allows the client to retry cleanly.
    /// </summary>
    Task ReleaseAsync(
        string idempotencyKey,
        string merchantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes idempotency records older than the configured retention period.
    /// Called by a background service on a schedule.
    /// </summary>
    Task CleanupExpiredAsync(CancellationToken cancellationToken);
}
