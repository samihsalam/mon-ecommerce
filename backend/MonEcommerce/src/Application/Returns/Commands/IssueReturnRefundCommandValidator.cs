using FluentValidation;

namespace MonEcommerce.Application.Returns.Commands;

public class IssueReturnRefundCommandValidator : AbstractValidator<IssueReturnRefundCommand>
{
    public IssueReturnRefundCommandValidator()
    {
        RuleFor(x => x.ReturnId).NotEmpty();
    }
}
