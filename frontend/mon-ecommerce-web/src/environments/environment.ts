export const environment = {
  production: false,
  sentryDsn: '',
  apiUrl: 'http://localhost:5287',
  // Mirrors the backend's Frontend:BaseUrl (Story 3.6) — used to build absolute canonical/Open
  // Graph/JSON-LD URLs during SSR, where window.location isn't available.
  siteUrl: 'http://localhost:4200',
};
