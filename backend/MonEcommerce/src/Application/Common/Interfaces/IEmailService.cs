namespace MonEcommerce.Application.Common.Interfaces;

public interface IEmailService
{
    // eventType (Story 5.4, AC #2): a stable identifier for the triggering domain event (e.g.
    // "OrderPlaced"), logged alongside the send outcome — see SendGridEmailService's
    // implementation for the retry/logging behavior. Callers see the same contract as before:
    // returns normally on eventual success (after up to 3 internal attempts), throws on total
    // failure — no handler needs to change its own try/catch to use this.
    Task SendAsync(string to, string subject, string htmlBody, string eventType, CancellationToken ct = default);
}
