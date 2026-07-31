using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Returns.Commands;

public class UpdateReturnStatusCommandHandler : IRequestHandler<UpdateReturnStatusCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public UpdateReturnStatusCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task Handle(UpdateReturnStatusCommand request, CancellationToken cancellationToken)
    {
        // No user-scoping — admin-wide, same as UpdateOrderStatusCommandHandler (Story 5.2).
        var returnRequest = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Return), request.ReturnId);

        if (returnRequest.Status != ReturnStatus.Pending)
        {
            throw new ConflictException("Cette demande de retour a déjà été traitée.");
        }

        returnRequest.Status = request.NewStatus;

        var customerEmail = await _identityService.GetEmailAsync(returnRequest.UserId) ?? string.Empty;
        returnRequest.AddDomainEvent(new ReturnStatusUpdatedEvent(
            returnRequest.Id,
            returnRequest.OrderId,
            customerEmail,
            ReturnStatusLabelFormatter.Format(request.NewStatus)));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
