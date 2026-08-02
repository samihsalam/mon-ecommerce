using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class UpdateStockCommandValidator : AbstractValidator<UpdateStockCommand>
{
    public UpdateStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        // AC #3's exact message.
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("Le stock ne peut pas être négatif");
        RuleFor(x => x.AlertThreshold).GreaterThanOrEqualTo(0);
    }
}
