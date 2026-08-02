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

        // AC #4: a rejection must include a reason to email the customer.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.NewStatus == ReturnStatus.Rejected)
            .WithMessage("Un motif est requis pour refuser une demande de retour.");
    }
}
