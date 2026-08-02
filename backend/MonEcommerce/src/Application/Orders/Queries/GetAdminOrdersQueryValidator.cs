using FluentValidation;

namespace MonEcommerce.Application.Orders.Queries;

public class GetAdminOrdersQueryValidator : AbstractValidator<GetAdminOrdersQuery>
{
    private const int MaxSearchLength = 200;

    public GetAdminOrdersQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0);

        RuleFor(x => x.Search)
            .MaximumLength(MaxSearchLength)
            .When(x => !string.IsNullOrEmpty(x.Search));

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("dateTo doit être postérieure ou égale à dateFrom.");
    }
}
