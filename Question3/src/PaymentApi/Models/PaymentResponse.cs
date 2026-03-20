namespace PaymentApi.Models;

public sealed record PaymentResponse(
    Guid TransactionId,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp);
