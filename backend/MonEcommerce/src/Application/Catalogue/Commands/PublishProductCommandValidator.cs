using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class PublishProductCommandValidator : AbstractValidator<PublishProductCommand>
{
    public PublishProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
