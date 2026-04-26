using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using Stripe;

namespace MonEcommerce.Infrastructure.ExternalServices;

public class StripePaymentService : IPaymentService
{
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;

    public StripePaymentService(PaymentIntentService paymentIntentService, RefundService refundService)
    {
        _paymentIntentService = paymentIntentService;
        _refundService = refundService;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(long amountInCents, string currency = "eur", CancellationToken ct = default)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = currency,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
        };

        var intent = await _paymentIntentService.CreateAsync(options, cancellationToken: ct);
        return new PaymentIntentResult(intent.ClientSecret, intent.Id);
    }

    public async Task<string> CreateRefundAsync(string paymentIntentId, long? amountInCents = null, CancellationToken ct = default)
    {
        var options = new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = amountInCents
        };

        var refund = await _refundService.CreateAsync(options, cancellationToken: ct);
        return refund.Id;
    }
}
