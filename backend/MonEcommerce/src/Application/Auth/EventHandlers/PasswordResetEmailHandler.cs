using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Auth.EventHandlers;

public class PasswordResetEmailHandler : INotificationHandler<PasswordResetRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<PasswordResetEmailHandler> _logger;

    public PasswordResetEmailHandler(IEmailService emailService, ILogger<PasswordResetEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(PasswordResetRequestedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var htmlBody = EmailTemplateBuilder.Wrap(
                "Réinitialisation du mot de passe",
                $"""
                <p>Bonjour {notification.Name},</p>
                <p>Vous avez demandé la réinitialisation de votre mot de passe MonEcommerce. Ce lien est valable 1 heure :</p>
                {EmailTemplateBuilder.Button(notification.ResetLink, "Réinitialiser mon mot de passe")}
                <p>Si vous n'êtes pas à l'origine de cette demande, vous pouvez ignorer cet email.</p>
                """);

            await _emailService.SendAsync(
                notification.Email,
                "Réinitialisation de votre mot de passe",
                htmlBody,
                "PasswordResetRequested",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send password reset email for user {UserId}", notification.UserId);
        }
    }
}
