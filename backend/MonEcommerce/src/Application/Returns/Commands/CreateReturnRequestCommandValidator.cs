using FluentValidation;

namespace MonEcommerce.Application.Returns.Commands;

public class CreateReturnRequestCommandValidator : AbstractValidator<CreateReturnRequestCommand>
{
    public CreateReturnRequestCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
    }
}
