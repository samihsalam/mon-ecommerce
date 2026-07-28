using FluentValidation;
using MonEcommerce.Application.Shipping;

namespace MonEcommerce.Application.Payments.Commands;

public class CreatePaymentIntentCommandValidator : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentCommandValidator()
    {
        RuleFor(x => x.ShippingOptionId)
            .NotEmpty()
            .Must(id => ShippingOptionsCatalog.TryGetById(id, out _))
            .WithMessage("Option de livraison inconnue.");

        // FluentValidation's NotEmpty() already rejects whitespace-only strings for string
        // properties (unlike Angular's own Validators.required, which needed a custom
        // requiredNotBlank validator — Story 4.3's review finding) — no extra rule needed here.
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.PostalCode).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();
    }
}
