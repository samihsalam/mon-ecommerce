using MonEcommerce.Application.Common.Security;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Commands;

// No CartOwner/UserId parameter — resolved via IUser inside the handler, same convention as
// CreatePaymentIntentCommand (Story 4.6): a return request is only ever made by an authenticated
// customer about their own order.
[Authorize]
public record CreateReturnRequestCommand(
    Guid OrderId,
    ReturnReason Reason,
    string Description,
    IReadOnlyList<ReturnPhotoUpload> Photos) : IRequest<CreateReturnRequestResponse>;
