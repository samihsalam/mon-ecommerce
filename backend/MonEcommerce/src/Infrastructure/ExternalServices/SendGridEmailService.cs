using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MonEcommerce.Infrastructure.ExternalServices;

public class SendGridEmailService : IEmailService
{
    private const int MaxAttempts = 3;

    private readonly ISendGridClient _client;
    // Factory-created, independent DbContext for the audit-log write — NOT the ambient scoped
    // IApplicationDbContext. See DependencyInjection.cs's registration comment: this handler may
    // run mid-way through the ambient context's own SaveChangesAsync (dispatched from
    // DispatchDomainEventsInterceptor), so reusing it here would be an unsafe reentrant call.
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        ISendGridClient client,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<SendGridEmailService> logger)
    {
        _client = client;
        _dbContextFactory = dbContextFactory;
        _fromEmail = configuration["SendGrid:FromEmail"]!;
        _fromName = configuration["SendGrid:FromName"] ?? "MonEcommerce";
        _logger = logger;
    }

    // Retries up to MaxAttempts times (Story 5.4, AC #4) and logs exactly one EmailDispatchLog
    // row per logical send, whatever it took to resolve (AC #2). Callers see the same contract
    // as before this story: returns normally on eventual success, throws on total failure — no
    // handler needs its own retry logic or awareness that this happens underneath.
    public async Task SendAsync(string to, string subject, string htmlBody, string eventType, CancellationToken ct = default)
    {
        string? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var message = new SendGridMessage
                {
                    From = new EmailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    HtmlContent = htmlBody
                };
                message.AddTo(new EmailAddress(to));

                var response = await _client.SendEmailAsync(message, ct);

                if (response.IsSuccessStatusCode)
                {
                    // SendGrid's send endpoint returns 202 Accepted with an empty body — the
                    // message id is header-only, not in a JSON response.
                    var messageId = response.Headers.TryGetValues("X-Message-Id", out var values)
                        ? values.FirstOrDefault()
                        : null;

                    await LogDispatchAsync(eventType, to, success: true, attempt, messageId, errorMessage: null, ct);
                    return;
                }

                var body = await response.Body.ReadAsStringAsync(ct);
                lastError = $"SendGrid error {response.StatusCode}: {body}";
                _logger.LogError("SendGrid send attempt {Attempt}/{MaxAttempts} failed: {Error}", attempt, MaxAttempts, lastError);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogError(ex, "SendGrid send attempt {Attempt}/{MaxAttempts} threw an exception", attempt, MaxAttempts);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct);
            }
        }

        await LogDispatchAsync(eventType, to, success: false, MaxAttempts, messageId: null, lastError, ct);
        throw new InvalidOperationException($"Failed to send email (event {eventType}) after {MaxAttempts} attempts: {lastError}");
    }

    private async Task LogDispatchAsync(string eventType, string recipient, bool success, int attemptCount, string? messageId, string? errorMessage, CancellationToken ct)
    {
        // A fresh, independent context per write — see the class-level comment on
        // _dbContextFactory for why the ambient scoped context can't safely be reused here.
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        context.EmailDispatchLogs.Add(new EmailDispatchLog
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Recipient = recipient,
            Success = success,
            AttemptCount = attemptCount,
            SendGridMessageId = messageId,
            ErrorMessage = errorMessage,
        });

        await context.SaveChangesAsync(ct);
    }
}
