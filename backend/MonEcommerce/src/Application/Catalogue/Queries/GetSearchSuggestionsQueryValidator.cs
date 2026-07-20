using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetSearchSuggestionsQueryValidator : AbstractValidator<GetSearchSuggestionsQuery>
{
    private const int MaxSearchLength = 200;

    public GetSearchSuggestionsQueryValidator()
    {
        // Checked against the trimmed length — see GetProductsQueryValidator for why a raw-length
        // check alone is bypassable by padding (" a" has raw length 2 but trims to 1 character).
        RuleFor(x => x.Search)
            .Must(s => !string.IsNullOrWhiteSpace(s) && s.Trim().Length is >= 2 and <= MaxSearchLength)
            .WithMessage($"Search must be between 2 and {MaxSearchLength} characters.");
    }
}
