using FluentValidation;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Orders.Commands;

public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();

        RuleFor(x => x.TrackingNumber)
            .NotEmpty()
            .When(x => x.NewStatus == OrderStatus.Shipped)
            .WithMessage("Le numéro de suivi est requis pour marquer une commande comme expédiée.");
    }
}
