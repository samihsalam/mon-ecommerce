using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class AddProductImageCommandValidator : AbstractValidator<AddProductImageCommand>
{
    public AddProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.FileName).NotEmpty().When(x => x.File is not null);
    }
}
