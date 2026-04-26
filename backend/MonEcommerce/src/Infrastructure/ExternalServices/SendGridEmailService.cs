using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MonEcommerce.Infrastructure.ExternalServices;

public class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(ISendGridClient client, IConfiguration configuration, ILogger<SendGridEmailService> logger)
    {
        _client = client;
        _fromEmail = configuration["SendGrid:FromEmail"]!;
        _fromName = configuration["SendGrid:FromName"] ?? "MonEcommerce";
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new SendGridMessage
        {
            From = new EmailAddress(_fromEmail, _fromName),
            Subject = subject,
            HtmlContent = htmlBody
        };
        message.AddTo(new EmailAddress(to));

        var response = await _client.SendEmailAsync(message, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError("SendGrid error {StatusCode}: {Body}", response.StatusCode, body);
        }
    }
}
