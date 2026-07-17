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
