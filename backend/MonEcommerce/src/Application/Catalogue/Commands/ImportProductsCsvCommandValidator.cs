using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Commands;

public class ImportProductsCsvCommandValidator : AbstractValidator<ImportProductsCsvCommand>
{
    public ImportProductsCsvCommandValidator()
    {
        RuleFor(x => x.FileContent).NotNull();
    }
}
