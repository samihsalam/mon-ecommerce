using Microsoft.Extensions.Configuration;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Payments.Models;
using Stripe;

namespace MonEcommerce.Infrastructure.ExternalServices;

public class StripePaymentService : IPaymentService
{
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;
    private readonly IConfiguration _configuration;

    public StripePaymentService(PaymentIntentService paymentIntentService, RefundService refundService, IConfiguration configuration)
    {
        _paymentIntentService = paymentIntentService;
        _refundService = refundService;
        _configuration = configuration;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        long amountInCents,
        string currency = "eur",
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = currency,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = metadata != null ? new Dictionary<string, string>(metadata) : null,
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

    public WebhookEvent ParseWebhookEvent(string payload, string signatureHeader)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);
        }
        catch (StripeException)
        {
            // Rethrown as a plain Application-layer exception (not Stripe.net's own type) so
            // Web's ProblemDetailsExceptionHandler can map it to 400 without needing a Stripe.net
            // reference of its own — an unverified payload must never reach the caller as a raw,
            // unhandled 500, and it must never be processed as if it were genuine.
            throw new InvalidWebhookSignatureException("Signature de webhook invalide.");
        }

        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        var metadata = paymentIntent?.Metadata is { } m
            ? new Dictionary<string, string>(m)
            : new Dictionary<string, string>();

        return new WebhookEvent(stripeEvent.Type, paymentIntent?.Id, paymentIntent?.Amount, metadata);
    }
}
