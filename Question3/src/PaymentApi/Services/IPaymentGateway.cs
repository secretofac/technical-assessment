using PaymentApi.Models;

namespace PaymentApi.Services;

/// <summary>
/// Abstraction over the external payment gateway.
/// Implementations must be idempotent-aware: if the gateway supports
/// its own idempotency key, pass it through to prevent double-charge
/// even if our system retries at the gateway level.
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
