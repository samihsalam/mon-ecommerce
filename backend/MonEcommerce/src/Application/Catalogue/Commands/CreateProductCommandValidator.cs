using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty();
        // AC #5: price is a positive integer in cents, never negative or zero.
        RuleFor(x => x.PriceInCents).GreaterThan(0);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Material).MaximumLength(200);
        RuleFor(x => x.Color).MaximumLength(100);
        RuleFor(x => x.Dimensions).MaximumLength(200);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
    }
}
