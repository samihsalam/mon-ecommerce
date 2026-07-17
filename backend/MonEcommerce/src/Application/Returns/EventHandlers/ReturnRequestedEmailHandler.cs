using System.Net;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Returns.EventHandlers;

public class ReturnRequestedEmailHandler : INotificationHandler<ReturnRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ReturnRequestedEmailHandler> _logger;

    public ReturnRequestedEmailHandler(IEmailService emailService, ILogger<ReturnRequestedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(ReturnRequestedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var reason = WebUtility.HtmlEncode(notification.Reason);
            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Votre demande de retour a été reçue",
                $"Nous avons bien reçu votre demande de retour pour la commande {notification.OrderId} (motif : {reason}).",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send return requested email for return {ReturnId}", notification.ReturnId);
        }
    }
}
