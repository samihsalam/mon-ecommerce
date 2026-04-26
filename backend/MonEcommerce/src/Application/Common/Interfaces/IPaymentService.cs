using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(long amountInCents, string currency = "eur", CancellationToken ct = default);
    Task<string> CreateRefundAsync(string paymentIntentId, long? amountInCents = null, CancellationToken ct = default);
}
