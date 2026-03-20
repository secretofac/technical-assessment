using PaymentApi.Models;

namespace PaymentApi.Services;

public interface IPaymentService
{
    Task<(PaymentResponse Response, int HttpStatusCode)> ProcessPaymentAsync(
        string merchantId,
        string idempotencyKey,
        PaymentRequest request,
        CancellationToken cancellationToken);
}
