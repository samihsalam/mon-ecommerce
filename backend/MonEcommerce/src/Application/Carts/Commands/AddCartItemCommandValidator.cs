using FluentValidation;

namespace MonEcommerce.Application.Carts.Commands;

public class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    // No prior upper bound let a request with an extreme quantity (e.g. int.MaxValue) reach
    // CartService unopposed — PriceInCents * Quantity is plain, unchecked int arithmetic (wraps
    // silently to a garbage/negative total) and items.Sum(...) uses checked int arithmetic
    // internally (throws OverflowException, which ProblemDetailsExceptionHandler doesn't map,
    // surfacing as a raw 500). 1,000 units of a single product is far beyond any realistic cart
    // and keeps PriceInCents * Quantity comfortably within int range for any real product price.
    public const int MaxQuantity = 1_000;

    public AddCartItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, MaxQuantity);
    }
}
