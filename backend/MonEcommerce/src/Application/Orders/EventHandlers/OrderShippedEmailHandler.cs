using System.Net;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Orders.EventHandlers;

public class OrderShippedEmailHandler : INotificationHandler<OrderShippedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderShippedEmailHandler> _logger;

    public OrderShippedEmailHandler(IEmailService emailService, ILogger<OrderShippedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var trackingNumber = WebUtility.HtmlEncode(notification.TrackingNumber);
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Votre commande a été expédiée",
                $"""
                <p>Votre commande {notification.OrderId} a été expédiée. Numéro de suivi : {trackingNumber}.</p>
                {EmailTemplateBuilder.Button(notification.TrackingLink, "Suivre ma commande")}
                """);

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Votre commande a été expédiée",
                htmlBody,
                "OrderShipped",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send order shipped email for order {OrderId} (tracking {TrackingNumber})", notification.OrderId, notification.TrackingNumber);
        }
    }
}
