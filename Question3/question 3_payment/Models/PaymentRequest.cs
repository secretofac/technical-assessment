namespace PaymentApi.Models;

/// <summary>
/// Inbound DTO for POST /api/payments.
/// PaymentMethodToken is tokenised — never raw card numbers.
/// </summary>
public sealed record PaymentRequest(
    decimal Amount,
    string Currency,
    string PaymentMethodToken,
    string? Description);
