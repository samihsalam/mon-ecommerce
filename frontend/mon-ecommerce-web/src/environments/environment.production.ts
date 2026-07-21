// Sentry DSNs are public, write-only identifiers (safe to commit — see Story 1.9 Dev Notes).
// Replace with the real DSN once a Sentry project exists for this app.
export const environment = {
  production: true,
  sentryDsn: '',
  // Replace with the real Railway backend URL once deployed (Story 1.9).
  apiUrl: '',
  // Replace with the real deployed frontend origin once deployed (Story 3.6) — mirrors the
  // backend's Frontend:BaseUrl.
  siteUrl: '',
};
