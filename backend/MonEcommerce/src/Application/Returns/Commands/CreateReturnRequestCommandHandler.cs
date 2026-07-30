using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Returns.Commands;

public class CreateReturnRequestCommandHandler : IRequestHandler<CreateReturnRequestCommand, CreateReturnRequestResponse>
{
    private static readonly TimeSpan ReturnWindow = TimeSpan.FromDays(14);

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IIdentityService _identityService;
    private readonly TimeProvider _timeProvider;
    private readonly IUser _user;

    public CreateReturnRequestCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IIdentityService identityService,
        TimeProvider timeProvider,
        IUser user)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _identityService = identityService;
        _timeProvider = timeProvider;
        _user = user;
    }

    public async Task<CreateReturnRequestResponse> Handle(CreateReturnRequestCommand request, CancellationToken cancellationToken)
    {
        // Ownership scoped in the query itself, not a separate check after an unscoped load —
        // established IDOR-prevention pattern (Story 2.5, 4.6).
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == _user.Id!, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Order), request.OrderId);

        // Order.LastModified stands in for "date delivered" — no dedicated DeliveredAt field
        // exists yet, and nothing in this codebase transitions Order.Status away from Pending
        // today (Story 7.2 not built). See Story 5.1's Dev Notes.
        var deliveredWithinWindow = order.Status == OrderStatus.Delivered
            && _timeProvider.GetUtcNow() - order.LastModified <= ReturnWindow;

        if (!deliveredWithinWindow)
        {
            throw new ReturnWindowExpiredException(
                "Cette commande n'est pas éligible à un retour (statut non livré ou délai de 14 jours dépassé).");
        }

        var photoUrls = new List<string>();
        foreach (var photo in request.Photos)
        {
            var result = await _fileStorageService.UploadAsync(photo.Content, photo.FileName, "returns", cancellationToken);
            photoUrls.Add(result.Url);
        }

        var returnRequest = new Return
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = _user.Id!,
            Reason = request.Reason,
            Description = request.Description,
            PhotoUrls = photoUrls,
            Status = ReturnStatus.Pending,
        };

        var customerEmail = await _identityService.GetEmailAsync(_user.Id!);
        returnRequest.AddDomainEvent(new ReturnRequestedEvent(returnRequest.Id, order.Id, customerEmail ?? string.Empty, request.Reason.ToString()));

        _context.Returns.Add(returnRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateReturnRequestResponse(returnRequest.Id, returnRequest.Status.ToString());
    }
}
