namespace MonEcommerce.Application.Common.Models;

public record PaymentIntentResult(string ClientSecret, string PaymentIntentId);
