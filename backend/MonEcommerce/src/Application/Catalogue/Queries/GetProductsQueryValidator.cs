using FluentValidation;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    // A public, unauthenticated endpoint with no length cap lets a caller submit an arbitrarily
    // large Search value — with no full-text index, that's a leading-wildcard `LIKE '%...%'` scan
    // over the whole catalogue per request. 200 chars comfortably covers any real product name or
    // description search while bounding the worst case.
    private const int MaxSearchLength = 200;

    public GetProductsQueryValidator()
    {
        // An empty/omitted Search is treated as "no search" (no rule fires). A term IS present
        // but shorter than 2 chars is rejected outright — there's no sensible way to "clamp" a
        // too-short search term the way pagination values are clamped in ProductCatalogueService.
        //
        // Checked against the TRIMMED length, not the raw length: ProductCatalogueService always
        // trims before using the term (see BuildCacheKey/GetProductsAsync), so a raw MinimumLength
        // check alone let a padded term like " a" (raw length 2) through, silently collapsing to a
        // 1-character search once trimmed — defeating the 2-char floor this rule exists to enforce.
        // A whitespace-only term (raw length 2+, trimmed length 0) is rejected by the same check,
        // rather than silently falling back to the unfiltered catalogue.
        RuleFor(x => x.Search)
            .Must(s => s!.Trim().Length is >= 2 and <= MaxSearchLength)
            .When(x => !string.IsNullOrEmpty(x.Search))
            .WithMessage($"Search must be between 2 and {MaxSearchLength} characters.");
    }
}
