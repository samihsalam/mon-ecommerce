using Microsoft.Extensions.Logging;
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
            var htmlBody = $"""
                <div style="font-family: 'DM Sans', Arial, sans-serif; color: #111111; max-width: 480px; margin: 0 auto;">
                  <h1 style="font-family: 'Cormorant Garamond', Georgia, serif; font-size: 28px;">Réinitialisation du mot de passe</h1>
                  <p>Bonjour {notification.Name},</p>
                  <p>Vous avez demandé la réinitialisation de votre mot de passe MonEcommerce. Ce lien est valable 1 heure :</p>
                  <p>
                    <a href="{notification.ResetLink}" style="display: inline-block; background-color: #C9A96E; color: #111111; padding: 12px 24px; border-radius: 4px; text-decoration: none; font-weight: 600;">
                      Réinitialiser mon mot de passe
                    </a>
                  </p>
                  <p>Si vous n'êtes pas à l'origine de cette demande, vous pouvez ignorer cet email.</p>
                </div>
                """;

            await _emailService.SendAsync(notification.Email, "Réinitialisation de votre mot de passe", htmlBody, cancellationToken);
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
