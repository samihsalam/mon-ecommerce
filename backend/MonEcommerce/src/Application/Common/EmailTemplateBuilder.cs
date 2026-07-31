namespace MonEcommerce.Application.Common;

// Extracted from PasswordResetEmailHandler (Story 2.3) — the only handler that already had a
// real HTML template ("Élégance Naturelle": DM Sans body, Cormorant Garamond heading, #C9A96E
// gold accent) before Story 5.4. Every transactional email handler now shares this one shell
// instead of each hand-rolling its own copy of the same wrapper markup.
public static class EmailTemplateBuilder
{
    public static string Wrap(string heading, string bodyHtml) => $"""
        <div style="font-family: 'DM Sans', Arial, sans-serif; color: #111111; max-width: 480px; margin: 0 auto;">
          <h1 style="font-family: 'Cormorant Garamond', Georgia, serif; font-size: 28px;">{heading}</h1>
          {bodyHtml}
        </div>
        """;

    // For the common "a paragraph, then a gold CTA button" shape (password reset, order
    // tracking link) — kept as a small helper rather than duplicated inline styling per handler.
    public static string Button(string href, string label) => $"""
        <p>
          <a href="{href}" style="display: inline-block; background-color: #C9A96E; color: #111111; padding: 12px 24px; border-radius: 4px; text-decoration: none; font-weight: 600;">
            {label}
          </a>
        </p>
        """;
}
