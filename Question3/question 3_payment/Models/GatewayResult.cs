namespace PaymentApi.Models;

/// <summary>
/// Result returned from the external payment gateway.
/// </summary>
public sealed record GatewayResult(
    bool Succeeded,
    string GatewayTransactionId,
    string? FailureReason);
