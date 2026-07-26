export const environment = {
  production: false,
  sentryDsn: '',
  apiUrl: 'http://localhost:5287',
  // Mirrors the backend's Frontend:BaseUrl (Story 3.6) — used to build absolute canonical/Open
  // Graph/JSON-LD URLs during SSR, where window.location isn't available.
  siteUrl: 'http://localhost:4200',
  // Publishable keys are safe to commit (Stripe docs) — but empty here since no Stripe account
  // is configured in this environment (Story 4.5's Dev Notes; the backend's Stripe:SecretKey is
  // similarly empty in appsettings.json). Replace with a real test-mode key to exercise checkout
  // step 3 locally.
  stripePublishableKey: '',
};
