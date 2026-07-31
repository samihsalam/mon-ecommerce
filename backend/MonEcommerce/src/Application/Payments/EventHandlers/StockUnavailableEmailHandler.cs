using System.Globalization;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Payments.EventHandlers;

public class StockUnavailableEmailHandler : INotificationHandler<StockUnavailableEvent>
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IEmailService _emailService;
    private readonly ILogger<StockUnavailableEmailHandler> _logger;

    public StockUnavailableEmailHandler(IEmailService emailService, ILogger<StockUnavailableEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(StockUnavailableEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var amount = (notification.AmountInCents / 100m).ToString("C", FrenchCulture);
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Votre paiement a été remboursé",
                $"<p>Un ou plusieurs articles de votre commande ne sont plus disponibles. Votre paiement de {amount} a été intégralement remboursé.</p>");

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Votre paiement a été remboursé",
                htmlBody,
                "StockUnavailable",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send stock unavailable email to {CustomerEmail}", notification.CustomerEmail);
        }
    }
}
