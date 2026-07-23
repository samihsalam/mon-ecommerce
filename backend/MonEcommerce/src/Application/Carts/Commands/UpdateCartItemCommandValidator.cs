using FluentValidation;

namespace MonEcommerce.Application.Carts.Commands;

public class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        // Same overflow-prevention upper bound as AddCartItemCommandValidator — see its comment.
        RuleFor(x => x.Quantity).InclusiveBetween(0, AddCartItemCommandValidator.MaxQuantity);
    }
}
