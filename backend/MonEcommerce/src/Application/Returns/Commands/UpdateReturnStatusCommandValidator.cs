using FluentValidation;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Commands;

public class UpdateReturnStatusCommandValidator : AbstractValidator<UpdateReturnStatusCommand>
{
    public UpdateReturnStatusCommandValidator()
    {
        RuleFor(x => x.ReturnId).NotEmpty();

        // Pending/Refunded aren't valid admin-initiated transitions through this endpoint —
        // Pending is the starting state, and Refunded is only ever set by
        // IssueReturnRefundCommandHandler after a real Stripe refund succeeds.
        RuleFor(x => x.NewStatus)
            .Must(s => s is ReturnStatus.Approved or ReturnStatus.Rejected)
            .WithMessage("Le statut doit être 'Approved' ou 'Rejected'.");
    }
}
