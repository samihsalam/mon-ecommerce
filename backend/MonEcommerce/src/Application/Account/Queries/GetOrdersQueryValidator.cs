using FluentValidation;

namespace MonEcommerce.Application.Account.Queries;

public class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public GetOrdersQueryValidator()
    {
        // Web/Endpoints/Account.cs binds page/pageSize straight from the query string with no
        // clamping — without this, page <= 0 computes a negative Skip(), which SQL Server
        // rejects at runtime (500), and an unbounded pageSize lets a caller force an arbitrarily
        // large result set. The EF Core InMemory provider used by AccountServiceOrdersTests
        // doesn't reproduce the negative-Skip failure, so this needed an explicit validator, not
        // just a test against InMemory.
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
