using PaymentApi.Models;

namespace PaymentApi.Services;

/// <summary>
/// Stub implementation for development/testing. Replace with real gateway
/// integration (Stripe, Adyen, etc.) in production. The real implementation
/// would use IHttpClientFactory to make outbound calls with resilience policies.
/// </summary>
public sealed class StubPaymentGateway : IPaymentGateway
{
    private readonly ILogger<StubPaymentGateway> _logger;

    public StubPaymentGateway(ILogger<StubPaymentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<GatewayResult> ChargeAsync(
        string merchantId,
        decimal amount,
        string currency,
        string paymentMethodToken,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Simulate network latency
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);

        _logger.LogInformation(
            "Gateway charge executed for merchant {MerchantId}, amount {Amount} {Currency}, token {TokenSuffix}",
            merchantId,
            amount,
            currency,
            MaskToken(paymentMethodToken));

        var gatewayTransactionId = Guid.NewGuid().ToString("N");

        return new GatewayResult(
            Succeeded: true,
            GatewayTransactionId: gatewayTransactionId,
            FailureReason: null);
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 4)
            return "****";

        return string.Concat("****", token.AsSpan(token.Length - 4));
    }
}
