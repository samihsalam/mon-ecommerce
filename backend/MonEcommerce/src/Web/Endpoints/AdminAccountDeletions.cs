using MediatR;
using MonEcommerce.Application.Account.Commands;
using MonEcommerce.Application.Account.Queries;

namespace MonEcommerce.Web.Endpoints;

public class AdminAccountDeletions : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/account-deletions";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is each command/query's own [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour — same split as AdminReturns.cs.
        groupBuilder.MapGet(GetAccountDeletionRequests, "").RequireAuthorization();
        groupBuilder.MapPost(ProcessAccountDeletion, "{requestId:guid}/process").RequireAuthorization();
    }

    [EndpointSummary("List pending account deletion requests (admin only)")]
    public static async Task<IResult> GetAccountDeletionRequests(ISender sender)
    {
        var result = await sender.Send(new GetAccountDeletionRequestsQuery());
        return Results.Ok(result);
    }

    [EndpointSummary("Anonymize a customer's personal data for a pending deletion request (admin only)")]
    public static async Task<IResult> ProcessAccountDeletion(Guid requestId, ISender sender)
    {
        await sender.Send(new ProcessAccountDeletionCommand(requestId));
        return Results.NoContent();
    }
}
