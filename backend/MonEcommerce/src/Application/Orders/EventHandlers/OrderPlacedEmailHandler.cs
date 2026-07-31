using System.Globalization;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Orders.EventHandlers;

public class OrderPlacedEmailHandler : INotificationHandler<OrderPlacedEvent>
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IEmailService _emailService;
    private readonly ILogger<OrderPlacedEmailHandler> _logger;

    public OrderPlacedEmailHandler(IEmailService emailService, ILogger<OrderPlacedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var total = (notification.TotalInCents / 100m).ToString("C", FrenchCulture);
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Confirmation de votre commande",
                $"<p>Votre commande {notification.OrderId} d'un montant de {total} a bien été enregistrée.</p>");

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Confirmation de votre commande",
                htmlBody,
                "OrderPlaced",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send order placed email for order {OrderId}", notification.OrderId);
        }
    }
}
