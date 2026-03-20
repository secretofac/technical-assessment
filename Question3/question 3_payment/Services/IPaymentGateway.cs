using PaymentApi.Models;

namespace PaymentApi.Services;

/// <summary>
/// Abstraction over the external payment gateway.
/// Implementations should forward the idempotency key to the gateway
/// to prevent double-charge at the gateway level.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(
        string merchantId,
        decimal amount,
        string currency,
        string paymentMethodToken,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
