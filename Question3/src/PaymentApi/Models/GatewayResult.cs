namespace PaymentApi.Models;

public sealed record GatewayResult(
    bool Succeeded,
    string GatewayTransactionId,
    string? FailureReason);
