using PaymentApi.Models;

namespace PaymentApi.Services;

/// <summary>
/// Stub for development/testing. Replace with real gateway (Stripe, Adyen, etc.)
/// using IHttpClientFactory in production.
/// </summary>
public sealed class StubPaymentGateway : IPaymentGateway
{
    private readonly ILogger<StubPaymentGateway> _logger;

    public StubPaymentGateway(ILogger<StubPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<GatewayResult> ChargeAsync(
        string merchantId,
        decimal amount,
        string currency,
        string paymentMethodToken,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // TODO: Simulate gateway call
        //   - Simulate latency
        //   - Log with masked token (never raw card data)
        //   - Return success result
        throw new NotImplementedException();
    }
}
