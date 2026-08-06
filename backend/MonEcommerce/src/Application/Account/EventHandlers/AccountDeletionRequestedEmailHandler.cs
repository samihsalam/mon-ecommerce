using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Account.EventHandlers;

public class AccountDeletionRequestedEmailHandler : INotificationHandler<AccountDeletionRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountDeletionRequestedEmailHandler> _logger;

    public AccountDeletionRequestedEmailHandler(IEmailService emailService, ILogger<AccountDeletionRequestedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(AccountDeletionRequestedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Votre demande de suppression de compte a été reçue",
                "<p>Nous avons bien reçu votre demande de suppression de compte. Un administrateur la traitera sous 30 jours.</p>");

            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Votre demande de suppression de compte a été reçue",
                htmlBody,
                "AccountDeletionRequested",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send account deletion requested email for request {RequestId}", notification.RequestId);
        }
    }
}
