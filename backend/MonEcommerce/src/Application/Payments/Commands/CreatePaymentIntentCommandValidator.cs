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
    }
}
