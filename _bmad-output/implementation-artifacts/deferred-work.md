# Deferred Work

## Deferred from: code review de 1-1-initialisation-des-3-projets (2026-04-14)

- Bearer transformer n'attache pas les security requirements aux opérations OpenAPI — à corriger dans Story 1.4 (JWT Auth infrastructure)
- Bearer scheme name `"Bearer"` vs `"BearerScheme"` (IdentityConstants) — configuration auth correcte dans Story 1.4
- `InitialiseDatabaseAsync` ignorée en production — remplacer par startup migration dans Story 1.3
- `EnsureDeletedAsync` + `EnsureCreatedAsync` en dev — remplacer par migrations EF Core dans Story 1.3
- Credentials admin hardcodés dans SeedAsync (`administrator@localhost` / `Administrator1!`) — à sécuriser dans Story 1.3
- ServiceDefaults (Aspire) orphelin dans le dépôt — nettoyage lors d'une story de maintenance
- `flutter build apk` non exécuté (Android SDK absent) — à valider manuellement quand Android SDK disponible
- SSR `maxAge: '1y'` pour tous les assets y compris index.html — configuration Cache-Control dans Story 3.x
- MappingTests `RuntimeHelpers.GetUninitializedObject` — à remplacer par tests domain-spécifiques dans Story 1.3+

## Deferred from: code review of story-1-7-infrastructure-domain-events (2026-07-06)

- No retry/dead-letter path for failed transactional emails (all 4 event handlers) — explicitly in scope of Story 5.4 ("failed email deliveries are retried, max 3 attempts"), not this story
- No validation on `CustomerEmail` and no guard on non-positive/zero monetary fields (`TotalInCents`/`AmountInCents`) on the domain event payloads — pre-existing by design: no caller constructs these events yet (pure infrastructure story), so payload validation belongs to the Epic 4/5 stories that will actually raise them from real Order/Refund entities where those invariants are already enforced
- `BaseEvent` record conversion gives domain events structural (value) equality instead of reference equality, so `BaseEntity.RemoveDomainEvent` could remove the wrong instance if two value-identical events are ever queued — not reachable today (no caller of `AddDomainEvent`/`RemoveDomainEvent` exists yet); revisit when the first Epic 4/5 story wires a real entity to raise these events (options: add a distinguishing `EventId`, or override equality to reference semantics)

## Deferred from: dev-story of 1-8-design-system-foundation (2026-07-06)

- `flutter analyze` / `flutter test` not run for `design_tokens.dart`, `app_theme.dart`, `main.dart` changes, and `test/app/theme/app_theme_test.dart` — Flutter/Dart SDK still absent from this machine (same root cause as the `flutter build apk` gap logged in Story 1.1). Code was hand-written and carefully reviewed (Flutter API signatures cross-checked against official docs, e.g. `CardThemeData` vs deprecated `CardTheme`) but not tool-verified. Validate manually once Flutter tooling is available on a dev machine.

## Deferred from: code review of story-1-8-design-system-foundation (2026-07-06)

- `pubspec.lock` was never regenerated for the new `google_fonts` dependency (confirmed via grep — no entry exists) — blocked by the same "Flutter/Dart SDK unavailable" gap above; run `flutter pub get` once tooling is available
- Google Fonts loaded directly from `fonts.googleapis.com`/`fonts.gstatic.com` CDN leaks visitor IPs to Google without consent — known GDPR exposure for this EU/French-facing site; revisit as part of Epic 8 (Conformité, Accessibilité & Qualité) which already owns RGPD/cookie-consent scope — self-hosting the two font families is the standard mitigation
- No automated check keeps the 3 independent hand-maintained design-token copies in sync (Angular `@theme` block, Angular plain `:root` block, Flutter `AppTokens`) — revisit once Story 1.9 (CI/CD pipeline) exists to host a cross-language parity check

## Deferred from: code review of story-1-9-cicd-pipeline-et-deploiement (2026-07-17)

- `TracesSampleRate = 1.0` (100% Sentry trace sampling) has no per-environment tuning — acceptable for a new, low-traffic project; revisit once real production traffic exists and Sentry quota becomes a concern
- `vercel.json`'s rewrites route all paths (including static JS/CSS/hashed assets) through the SSR serverless function, with no static-asset exclusion or `outputDirectory` specified — can't be verified without an actual live Vercel deployment; validate/fix once a real Vercel project is connected
- No CI job builds/verifies the Dockerfile itself — verified manually this session but not continuously checked; add a `docker` job to `ci.yml` in a follow-up

## Deferred from: dev-story of 2-1-inscription-client (2026-07-19)

- **Likely real startup bug**: `IEmailService` is only conditionally registered (Story 1.6) when `SendGrid:ApiKey` is configured. `WebApplicationBuilder` enables `ValidateOnBuild=true` in Development by default, which eagerly validates the *entire* DI graph — including all `INotificationHandler<T>` implementations (now 5, after this story) that depend on `IEmailService`. This means `dotnet run` in Development likely fails to start at all without a SendGrid key configured, undermining Story 1.6's "credentials-optional" design intent. Confirmed via `dotnet ef migrations add` hitting the same DI validation failure (EF design-time tooling builds the same host). Recommended fix: register a no-op/console-logging `IEmailService` fallback when no SendGrid key is present, rather than leaving the service unregistered — preserves the credentials-optional spirit while satisfying `ValidateOnBuild`.
- EF migration `AddApplicationUserName` was generated but not applied to a database — this environment cannot reach the `DESKTOP-M36577B` SQL Server instance referenced in `appsettings.json`. Run `dotnet ef database update --project src/Infrastructure --startup-project src/Web` once local SQL Server is confirmed running.
- Flutter code for this story (Riverpod `Notifier`/`NotifierProvider`, go_router, `flutter_secure_storage`, `dio`) was not verified by any tooling (`flutter analyze`/`flutter test`/`flutter pub get`) — same pre-existing SDK gap as Stories 1.1/1.8/1.9, but this story has a much larger unverified Flutter surface area than those. Prioritize getting Flutter tooling installed before the next mobile-heavy story.

## Deferred from: code review of story-8-2-banniere-rgpd-et-gestion-des-cookies (2026-08-06)

- No cross-tab consent synchronization (`storage` event listener) in `ConsentService` — low practical impact for a banner shown once per session; revisit if multi-tab consent drift is reported.
- `role="dialog" aria-modal="true"` on the cookie banner without `inert`/`aria-hidden` on background content — `cdkTrapFocus` only intercepts Tab-cycling, doesn't make siblings inert. Same pre-existing gap pattern as `CartDrawerComponent`; full modal semantics belong to Epic 8's dedicated accessibility stories (8.4/8.5).
- No `aria-describedby` linking the cookie banner dialog to its descriptive paragraph — minor a11y polish; Stories 8.4/8.5 own WCAG work for this epic.
- Fixed-position cookie banner has no explicit z-index/scroll-padding coordination with other fixed elements (`app-toast`, `app-cart-drawer`) — low practical risk (banner is dismissed on first interaction), no reported conflict.
- `CookieBannerComponent`'s focus-management `effect()`'s `setTimeout` has no cleanup if `isBannerOpen()` toggles rapidly — no user-triggered path in this story produces rapid toggling today.

## Deferred from: code review of story-8-3-droit-a-loubli-et-suppression-des-donnees (2026-08-06)

- No transaction spans `IIdentityService.AnonymizeUserAsync` (persists immediately via `UserManager`) and `ProcessAccountDeletionCommandHandler`'s own `SaveChangesAsync` — a late failure could leave the identity already anonymized while the audit trail (`Status`/`ProcessedByAdminUserId`/`ProcessedAt`) never gets stamped. Same unaddressed gap class as `AccountService.UpdateProfileAsync` (Story 2.4) already has for its own `UserManager` calls; revisit if/when this codebase adopts a general pattern for sharing a transaction between `UserManager` and `IApplicationDbContext`.
- TOCTOU race in `RequestAccountDeletionCommandHandler`'s idempotency guard (plain `AnyAsync` check, no unique/filtered index) — two concurrent submits could create two `Pending` rows for one user. Same class of gap as every other check-then-insert idempotency guard in this codebase.
- No concurrency token on `AccountDeletionRequest` — two admins processing the same request id concurrently could corrupt `ProcessedByAdminUserId`/`ProcessedAt`. `Return` has the identical gap, unaddressed.
- `ConflictException` (409) is overloaded for two different failures in `ProcessAccountDeletionCommandHandler` ("already processed" vs. `AnonymizeUserAsync` returning "user not found") — properly distinguishing them needs `ProcessAccountDeletionCommand` to return a `Result` instead of being a bare `IRequest`, a larger change than this edge case (only reachable if referential integrity is already broken) justifies today.

## Deferred from: code review of story-8-4-accessibilite-wcag-21-aa-formulaires-et-navigation (2026-08-06)

- No automated accessibility regression test (`axe-core`/`cypress-axe`/`pa11y`) exists anywhere in this codebase — the standard engineering-controllable substitute for AC #6's manual VoiceOver/TalkBack/NVDA testing. Adding one is a real, valuable follow-up (new devDependency + CI wiring) but larger in scope than this review pass's fixes; worth a dedicated future story.
