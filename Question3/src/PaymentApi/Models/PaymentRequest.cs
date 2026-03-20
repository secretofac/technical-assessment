namespace PaymentApi.Models;

public sealed record PaymentRequest(
    decimal Amount,
    string Currency,
    string PaymentMethodToken,
    string? Description);
