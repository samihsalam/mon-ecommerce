using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Orders.EventHandlers;

public class OrderDeliveredEmailHandler : INotificationHandler<OrderDeliveredEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderDeliveredEmailHandler> _logger;

    public OrderDeliveredEmailHandler(IEmailService emailService, ILogger<OrderDeliveredEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderDeliveredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Votre commande a été livrée",
                $"<p>Votre commande {notification.OrderId} a été livrée. Merci pour votre achat !</p>");

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Votre commande a été livrée",
                htmlBody,
                "OrderDelivered",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send order delivered email for order {OrderId}", notification.OrderId);
        }
    }
}
