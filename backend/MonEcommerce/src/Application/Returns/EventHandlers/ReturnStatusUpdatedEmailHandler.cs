using System.Net;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
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
            // Reason (Story 7.3, AC #4) is free-text admin input reflected into an email —
            // HTML-encoded, same convention as ReturnRequestedEmailHandler's own reason field.
            var body = $"<p>Votre demande de retour pour la commande {notification.OrderId} a été mise à jour : {notification.NewStatus}.</p>";
            if (!string.IsNullOrWhiteSpace(notification.Reason))
            {
                body += $"<p>Motif : {WebUtility.HtmlEncode(notification.Reason)}</p>";
            }

            var htmlBody = EmailTemplateBuilder.Wrap("Mise à jour de votre demande de retour", body);

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Mise à jour de votre demande de retour",
                htmlBody,
                "ReturnStatusUpdated",
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
