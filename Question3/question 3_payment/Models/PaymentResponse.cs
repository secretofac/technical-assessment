namespace PaymentApi.Models;

/// <summary>
/// Outbound DTO returned from POST /api/payments.
/// </summary>
public sealed record PaymentResponse(
    Guid TransactionId,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp);
