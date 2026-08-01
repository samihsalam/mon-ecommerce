using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class ReorderProductImagesCommandValidator : AbstractValidator<ReorderProductImagesCommand>
{
    public ReorderProductImagesCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ImageIds).NotEmpty();
    }
}
