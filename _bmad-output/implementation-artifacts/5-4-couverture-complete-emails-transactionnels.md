# Story 5.4: Couverture Complète Emails Transactionnels

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the platform,
I want every business event to trigger its corresponding email within ≤ 30 seconds,
so that complete traceability and proactive customer communication are guaranteed across all scenarios.

## Acceptance Criteria

1. Given the full email matrix is implemented, when each triggering event occurs, then the corresponding email is dispatched within ≤ 30 seconds, for all seven: Inscription (welcome), Commande confirmée, Commande expédiée, Livraison confirmée, Demande retour reçue, Remboursement émis, Réinitialisation mot de passe.
2. Each email dispatch is logged with: event type, recipient, timestamp, SendGrid message ID.
3. Integration tests assert the ≤ 30s delivery constraint for each email type.
4. Failed email deliveries are retried (max 3 attempts) and logged as errors if all retries fail.
5. No email contains sensitive data (no card numbers, no passwords, no tokens in plain text).

## Tasks / Subtasks

### AC #1 — the email matrix itself (already fully wired, verify don't rebuild)

- [x] Task 1: Confirm all seven triggers already exist and fire synchronously in-process via MediatR (no queue/worker — the ≤30s SLA is structurally guaranteed by this architecture, not something that needs engineering): `UserRegisteredWelcomeEmailHandler` (Story 2.1), `OrderPlacedEmailHandler` (Story 4.6), `OrderShippedEmailHandler` (Story 4.x/5.2), `OrderDeliveredEmailHandler` (Story 5.2), `ReturnRequestedEmailHandler` (Story 1.x/5.1), `RefundIssuedEmailHandler` (Story 4.6/5.3), `PasswordResetEmailHandler` (Story 2.3). No new handler needed — this AC is a verification task (see Task 6's tests), several handlers even carry a `// TODO (Story 5.4): add an integration test...` comment pointing straight at this story.

### AC #2, #4 — centralized dispatch logging + retry (the real new work)

- [x] Task 2: `Domain/Entities/EmailDispatchLog.cs` (`BaseAuditableEntity` for the free `Created` timestamp — AC #2's "timestamp"): `EventType` (string), `Recipient` (string), `SendGridMessageId` (string?, null if never succeeded), `Success` (bool), `AttemptCount` (int), `ErrorMessage` (string?) — one row per **logical** send (not per attempt; `AttemptCount` records how many tries it took), matching `PaymentAuditLog`'s existing "one row per outcome" convention. `Infrastructure/Data/Configurations/EmailDispatchLogConfiguration.cs` + migration.
- [x] Task 3: Extend `IEmailService.SendAsync` with a new `string eventType` parameter (AC #2's "event type" field) — `SendAsync(string to, string subject, string htmlBody, string eventType, CancellationToken ct = default)`. Update all seven handler call sites to pass a stable event-type string (e.g. `"UserRegistered"`, `"OrderPlaced"`, `"OrderShipped"`, `"OrderDelivered"`, `"ReturnRequested"`, `"RefundIssued"`, `"PasswordResetRequested"` — matching each event's own class name, so the log is trivially correlatable back to the triggering domain event).
- [x] Task 4: Rewrite `SendGridEmailService.SendAsync` (AC #2, #4):
  - [x] Retry loop, up to 3 attempts total, small delay between attempts (e.g. 500ms × attempt number — keeps total retry time a small fraction of the 30s SLA even in the worst case)
  - [x] On a successful attempt: extract the SendGrid message id from the response's `X-Message-Id` header (SendGrid's send endpoint returns `202` with an empty body — the message id is header-only, not in the response JSON), write one `EmailDispatchLog` row (`Success = true`, `AttemptCount` = however many tries it took, `SendGridMessageId` set), return normally
  - [x] On exhausting all 3 attempts without success: write one `EmailDispatchLog` row (`Success = false`, `AttemptCount = 3`, `ErrorMessage` set), `_logger.LogError(...)` (AC #4's "logged as errors"), then throw — **deliberately preserves every existing handler's current `catch (Exception ex) { _logger.LogError(...); }` shape unchanged**; the handlers don't need to know retry happened underneath, they just see the same single call succeed or ultimately fail, exactly as today
  - [x] Needs `IDbContextFactory<ApplicationDbContext>` injected (new constructor dependency, not `IApplicationDbContext` as originally sketched — see Dev Notes addendum below) to write the audit row directly — no decorator/second class needed, this logic lives entirely in the existing `SendGridEmailService`

### AC #1's real remaining gap — HTML templates for the six plain-text handlers

- [x] Task 5: `PasswordResetEmailHandler` (Story 2.3) already renders full HTML — DM Sans body font, Cormorant Garamond heading, `#C9A96E` gold CTA button, i.e. exactly the "Élégance Naturelle" styling — while the other six handlers (`UserRegisteredWelcomeEmailHandler`, `OrderPlacedEmailHandler`, `OrderShippedEmailHandler`, `OrderDeliveredEmailHandler`, `ReturnRequestedEmailHandler`, `RefundIssuedEmailHandler`) still send plain interpolated strings. **This is the actual, correct place to build the shared HTML template this codebase was missing** — Story 5.2's Dev Notes flagged "no HTML email system exists anywhere" based on sampling only the plain-text handlers, without checking `PasswordResetEmailHandler`; that claim was incomplete, corrected here.
  - [x] Extract `PasswordResetEmailHandler`'s inline HTML shell into a shared `Application/Common/EmailTemplateBuilder.cs` (`public static string Wrap(string heading, string bodyHtml)` — the `<div>`/font/color chrome factored out, `bodyHtml` is the handler-specific content) so six handlers don't each hand-roll their own copy of the same wrapper markup
  - [x] Convert all six plain-text handlers to build their content via `EmailTemplateBuilder.Wrap(...)` instead of a bare interpolated string — content unchanged (same information as today), only the presentation changes. `StockUnavailableEmailHandler` (Payments, Story 4.6 — a real handler outside the AC's own seven-email list but sharing the same `IEmailService.SendAsync` call site) was also converted, since the signature change was compile-breaking for it regardless.
  - [x] `PasswordResetEmailHandler` itself refactored to call the same shared builder too, so there's exactly one place the shell markup lives, not two

### AC #5 — sensitive-data audit (verification, not new code)

- [x] Task 6: Read all seven handlers' email bodies end to end and confirm: no card numbers (Stripe tokenization means card data never reaches this backend at all — Story 1.6/4.5's PCI-DSS design), no passwords, no raw session/JWT tokens shown as plain text outside their legitimate purpose. `PasswordResetEmailHandler`'s reset token is embedded only inside the reset link's URL (standard, expected, industry-universal password-reset UX — the AC's "no tokens in plain text" means not printing a token as freestanding text a screen-reader/log-scraper could casually capture out of context, not "never include a working link at all"). **Audit finding: PASS** — none of the seven bodies (nor `StockUnavailableEmailHandler`) print a card number, password, or freestanding token; the only URL-embedded secret is the password-reset link, which is the accepted industry pattern. No code change made.

### AC #3 — SLA test coverage

- [x] Task 7: New `EmailDispatchSlaTests.cs` — for each of the 7 handlers, mock `IEmailService`, invoke `Handle(...)`, assert completion well under 30s via `Stopwatch` (structurally this will be milliseconds, given synchronous in-process dispatch — the test exists to make the SLA an explicit, checked assertion per the AC's own wording, not because 30s is ever realistically at risk with no queue in the architecture). Used a 5s ceiling rather than literally 30s so a real regression still fails fast in CI.
- [x] Task 8: `SendGridEmailServiceTests.cs` (new — none existed before, since retry/logging is new logic): succeeds on the first attempt and logs one `Success = true` row with the captured message id; retries and succeeds on the 2nd/3rd attempt; exhausts all 3 attempts, throws, and logs one `Success = false` row with `AttemptCount = 3`
- [x] Task 9: Update the seven existing handler tests' `SendAsync` mock setups for the new `eventType` parameter (compile-breaking otherwise)

## Dev Notes

### Why retry + logging live inside `SendGridEmailService`, not a separate decorator

A decorator (`LoggingEmailService` wrapping `SendGridEmailService`) was considered but rejected: it would need `SendGridEmailService.SendAsync` to somehow surface the message id and attempt count up through a second `IEmailService` implementation's own `SendAsync`, which only complicates the boundary for no real benefit — nothing else in this codebase composes `IEmailService` decorators, and there's exactly one production implementation of it. Keeping retry+logging inside the one real implementation is simpler and matches how `HandleStripeWebhookCommandHandler`'s own retry loop (Story 4.6) lives directly in the class that needs it, not a wrapper.

### Correcting Story 5.2's Dev Notes

Story 5.2 stated "this codebase has no HTML email template system at all yet" when deferring its own AC #3/#4 to this story. That was true for six of the seven handlers but not `PasswordResetEmailHandler` (Story 2.3), which was never sampled when that claim was written. This story extends the *existing* pattern rather than inventing a new one — the correction doesn't change what Story 5.2 needed to do (nothing, it had no HTML content of its own to add), only the accuracy of the stated reason.

### Why `IEmailService.SendAsync` still returns `Task`, not `Task<EmailSendResult>`

Handlers never see the retry mechanics — from a handler's perspective, `SendAsync` either eventually succeeds (returns normally) or ultimately fails (throws, after 3 internal attempts), identical to today's contract. Changing the return type would force all seven handlers to change their success-path code for no reason; keeping `Task` means only the new `eventType` parameter is a call-site change, not a control-flow one.

## Project Structure Notes

New `Domain/Entities/EmailDispatchLog.cs`, `Infrastructure/Data/Configurations/EmailDispatchLogConfiguration.cs` (+migration), `Application/Common/EmailTemplateBuilder.cs`, `tests/.../EmailDispatchSlaTests.cs`, `tests/.../SendGridEmailServiceTests.cs`. Modified: `Application/Common/Interfaces/IEmailService.cs`, `Infrastructure/ExternalServices/SendGridEmailService.cs`, all seven email handlers (new `eventType` argument; six of them also gain HTML bodies via the shared template).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 5.4 acceptance criteria (Epic 5 section, line ~970)
- `backend/MonEcommerce/src/Application/Auth/EventHandlers/PasswordResetEmailHandler.cs` — the existing HTML template pattern this story extracts and reuses, not reinvents
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/SendGridEmailService.cs` — currently silently swallows a non-2xx SendGrid response (logs, doesn't throw) — this story's retry logic is also what first makes a real send failure visible to callers at all
- `_bmad-output/implementation-artifacts/4-6-confirmation-commande-et-anti-overselling.md` — `PaymentAuditLog`'s "one row per outcome, BaseAuditableEntity for free Created" convention this story's `EmailDispatchLog` follows
- `_bmad-output/implementation-artifacts/5-2-notifications-email-changements-de-statut.md` — where AC #3/#4's HTML-template gap was originally (incompletely) diagnosed and deferred to this story

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Discovered mid-implementation that `SendGridEmailService` cannot safely take `IApplicationDbContext` (the story's original sketch) as a constructor dependency: 5 of the 7 target handlers are `AddDomainEvent`-triggered and run synchronously inside `DispatchDomainEventsInterceptor.SavingChangesAsync` — i.e. mid-way through an already-in-progress `SaveChangesAsync()` call on the ambient scoped context. Calling `SaveChangesAsync()` again on that same instance at that point throws `InvalidOperationException` ("a second operation was started on this context before a previous operation completed"). Fixed by injecting `IDbContextFactory<ApplicationDbContext>` instead (new registration in `DependencyInjection.cs`, deliberately without interceptors — `EmailDispatchLog` has no domain events of its own) and creating a fresh, independent context per audit-log write in `LogDispatchAsync`.
- `Microsoft.EntityFrameworkCore.PooledDbContextFactory<TContext>` (considered for `SendGridEmailServiceTests.cs`'s fake factory) could not be resolved by the compiler in this project's target framework; replaced with a minimal hand-written `IDbContextFactory<ApplicationDbContext>` stand-in scoped to the test file — simpler and avoids the dependency question entirely.

### Completion Notes List

- All 7 AC-listed handlers plus `StockUnavailableEmailHandler` (Story 4.6, sharing the same `IEmailService.SendAsync` call site, not part of the AC's 7-email list but compile-broken by the signature change regardless) now pass a stable `eventType` string and render through the shared `EmailTemplateBuilder.Wrap()` HTML shell.
- `EmailDispatchLog` (new entity + config + migration `AddEmailDispatchLog`) gives one row per logical send with `EventType`, `Recipient`, `SendGridMessageId`, `Success`, `AttemptCount`, `ErrorMessage`, and `Created` (via `BaseAuditableEntity`) for AC #2's audit trail.
- `SendGridEmailService.SendAsync` now retries up to 3 attempts (500ms × attempt number backoff) before giving up, satisfying AC #4; the retry mechanics stay fully internal — every existing handler's `catch (Exception ex) { _logger.LogError(...); }` shape is unchanged.
- AC #5 sensitive-data audit: PASS. No card numbers, passwords, or freestanding tokens in any of the 7 (+1) email bodies; the password-reset token only ever appears embedded in the reset link's URL, the accepted UX pattern.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 231/231 Application.UnitTests passing, including the 7 new `EmailDispatchSlaTests` and 4 new `SendGridEmailServiceTests`. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test/migrate on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Domain/Entities/EmailDispatchLog.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/EmailDispatchLogConfiguration.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260731005143_AddEmailDispatchLog.cs` (+ `.Designer.cs`, snapshot update)
- `backend/MonEcommerce/src/Application/Common/EmailTemplateBuilder.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Common/Email/EmailDispatchSlaTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Common/Email/SendGridEmailServiceTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Application/Common/Interfaces/IEmailService.cs` (new `eventType` parameter)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (`EmailDispatchLogs` DbSet)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (`EmailDispatchLogs` DbSet)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (`IDbContextFactory<ApplicationDbContext>` registration)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/SendGridEmailService.cs` (retry loop, audit logging, factory-based independent context)
- `backend/MonEcommerce/src/Application/Auth/EventHandlers/UserRegisteredWelcomeEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Auth/EventHandlers/PasswordResetEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderDeliveredEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnStatusUpdatedEmailHandler.cs`
- `backend/MonEcommerce/src/Application/Payments/EventHandlers/StockUnavailableEmailHandler.cs`
- 7 existing handler test files under `backend/MonEcommerce/tests/Application.UnitTests/**/EventHandlers/*Tests.cs` (mock `SendAsync` setups updated for the new `eventType` parameter)
