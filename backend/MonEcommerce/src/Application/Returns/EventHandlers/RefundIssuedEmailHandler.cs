using System.Globalization;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Returns.EventHandlers;

public class RefundIssuedEmailHandler : INotificationHandler<RefundIssuedEvent>
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IEmailService _emailService;
    private readonly ILogger<RefundIssuedEmailHandler> _logger;

    public RefundIssuedEmailHandler(IEmailService emailService, ILogger<RefundIssuedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(RefundIssuedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var amount = (notification.AmountInCents / 100m).ToString("C", FrenchCulture);
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Confirmation de votre remboursement",
                $"""
                <p>Un remboursement de {amount} a été émis pour la commande {notification.OrderNumber}.
                Le montant sera crédité sur votre moyen de paiement d'origine sous 3 à 5 jours ouvrés.</p>
                """);

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Confirmation de votre remboursement",
                htmlBody,
                "RefundIssued",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send refund issued email for refund {RefundId}", notification.RefundId);
        }
    }
}
