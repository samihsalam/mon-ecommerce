using FluentValidation;

namespace MonEcommerce.Application.Returns.Queries;

public class GetAdminReturnsQueryValidator : AbstractValidator<GetAdminReturnsQuery>
{
    public GetAdminReturnsQueryValidator()
    {
        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("dateTo doit être postérieure ou égale à dateFrom.");
    }
}
