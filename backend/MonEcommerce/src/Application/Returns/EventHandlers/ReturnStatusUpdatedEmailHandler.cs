using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Returns.EventHandlers;

public class ReturnStatusUpdatedEmailHandler : INotificationHandler<ReturnStatusUpdatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ReturnStatusUpdatedEmailHandler> _logger;

    public ReturnStatusUpdatedEmailHandler(IEmailService emailService, ILogger<ReturnStatusUpdatedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(ReturnStatusUpdatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Mise à jour de votre demande de retour",
                $"Votre demande de retour pour la commande {notification.OrderId} a été mise à jour : {notification.NewStatus}.",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send return status updated email for return {ReturnId}", notification.ReturnId);
        }
    }
}
