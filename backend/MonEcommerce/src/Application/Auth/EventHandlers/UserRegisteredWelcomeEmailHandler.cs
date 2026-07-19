using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Auth.EventHandlers;

public class UserRegisteredWelcomeEmailHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<UserRegisteredWelcomeEmailHandler> _logger;

    public UserRegisteredWelcomeEmailHandler(IEmailService emailService, ILogger<UserRegisteredWelcomeEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                notification.Email,
                "Bienvenue chez MonEcommerce",
                $"Bonjour {notification.Name}, bienvenue ! Votre compte a été créé avec succès.",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send welcome email for user {UserId}", notification.UserId);
        }
    }
}
