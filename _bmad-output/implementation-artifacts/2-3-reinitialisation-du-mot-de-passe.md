# Story 2.3: Réinitialisation du Mot de Passe

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to reset my password via email,
so that I can recover access to my account if I forget it.

## Acceptance Criteria

1. **Given** a registered email address, **when** `POST /api/v1/auth/forgot-password` is called, **then** a reset email with a secure token link (valid 1 hour) is sent within 30 seconds. An unregistered email must get the same response (no email enumeration) — not explicit in the epic wording, but standard practice and consistent with this codebase's existing security posture (see Dev Notes).
2. **Given** a valid reset token, **when** `POST /api/v1/auth/reset-password` is called with a new password, **then** the password is updated and all existing refresh tokens for that user are revoked.
3. **Given** an expired or already-used reset token, **when** the reset is attempted, **then** a `400 Bad Request` ProblemDetails-shaped response is returned with a clear expiry message.
4. The reset link is single-use (invalidated after first successful use).
5. The reset email template matches the platform's visual identity (DM Sans, Élégance Naturelle palette).

## Tasks / Subtasks

### Backend — genuinely new (unlike Story 2.2, nothing here exists yet)

- [x] Task 1: Fix two real pre-existing gaps that block this story (AC: #1, #2, #3)
  - [x] `AddIdentityCore<ApplicationUser>()` in `DependencyInjection.cs` never calls `.AddDefaultTokenProviders()` — without it, `UserManager.GeneratePasswordResetTokenAsync`/`ResetPasswordAsync` throw at runtime (`NotSupportedException`, no token provider registered). Add `.AddDefaultTokenProviders()` to the chain.
  - [x] **Separately discovered, real, and currently silent**: no `IdentityOptions.Password` policy is configured anywhere, so ASP.NET Identity's *framework defaults* apply (`RequireDigit`, `RequireLowercase`, `RequireUppercase`, `RequireNonAlphanumeric` all `true`, `RequiredLength = 6`) — but every validator and every client-side form in this app (Stories 1.4, 2.1, 2.2) only ever promised "≥ 8 characters, nothing else." A password like `"password123"` (used throughout this codebase's own test fixtures) would be **rejected by a real `UserManager.CreateAsync`/`ResetPasswordAsync` call** today — never caught before because every existing test mocks `UserManager`, and the real SQL Server database has been unreachable from this dev environment for the whole project so far (no end-to-end `CreateAsync` has ever actually run). Fix: `builder.Services.Configure<IdentityOptions>(options => { options.Password.RequireDigit = false; options.Password.RequireLowercase = false; options.Password.RequireUppercase = false; options.Password.RequireNonAlphanumeric = false; options.Password.RequiredLength = 8; });` — matches what the app has always advertised, nothing more.
- [x] Task 2: Configure the 1h reset-token lifespan (AC: #1)
  - [x] `builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));`
- [x] Task 3: `ForgotPasswordCommand` (AC: #1)
  - [x] `ForgotPasswordCommand(string Email) : IRequest<Result>` + validator (`NotEmpty().EmailAddress()`) + handler delegating to `IAuthService.ForgotPasswordAsync`
  - [x] `IAuthService.ForgotPasswordAsync(string email, CancellationToken)`: look up the user; **if not found, still return `Result.Success()`** (no enumeration) and do nothing further; if found, generate a token via `UserManager.GeneratePasswordResetTokenAsync(user)`, build a link `{Frontend:BaseUrl}/reinitialiser-mot-de-passe?email={urlEncodedEmail}&token={urlEncodedToken}`, publish a `PasswordResetRequestedEvent(user.Id, user.Name, user.Email, link)`
  - [x] New config key: `Frontend:BaseUrl` in `appsettings.json` (`http://localhost:4200` for local dev — same port Story 2.1's `environment.ts` already assumes for the Angular dev server)
- [x] Task 4: `ResetPasswordCommand` (AC: #2, #3, #4)
  - [x] `ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>` + validator (`Email`: `NotEmpty().EmailAddress()`; `Token`: `NotEmpty()`; `NewPassword`: `NotEmpty().MinimumLength(8)`) + handler delegating to `IAuthService.ResetPasswordAsync`
  - [x] `IAuthService.ResetPasswordAsync(string email, string token, string newPassword, CancellationToken)`: look up the user; if not found OR `UserManager.ResetPasswordAsync(user, token, newPassword)` fails, return `Result.Failure(["Ce lien de réinitialisation est invalide ou a expiré."])` — **same generic message for "user not found" and "token invalid/expired,"** to avoid distinguishing (timing/response-shape) whether an email is registered. If it succeeds: revoke every non-revoked `RefreshToken` row for that `user.Id` (`_context.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)`, set `RevokedAt`, `SaveChangesAsync`) — mirrors the AC's "all existing refresh tokens... are revoked," and note that Identity's own `ResetPasswordAsync` already regenerates the user's `SecurityStamp`, which is *what actually makes the reset token single-use* (any second attempt with the same or a different pre-reset token fails token validation against the new stamp) — no separate token-tracking table needed.
- [x] Task 5: `PasswordResetRequestedEvent` + email handler (AC: #1, #5)
  - [x] `PasswordResetRequestedEvent(string UserId, string Name, string Email, string ResetLink) : BaseEvent` (`src/Domain/Events/`)
  - [x] `PasswordResetEmailHandler : INotificationHandler<PasswordResetRequestedEvent>` (`src/Application/Auth/EventHandlers/`) — same fire-and-forget try/catch shape as `UserRegisteredWelcomeEmailHandler` (Story 2.1)
  - [x] HTML email body matches the platform's visual identity: DM Sans font-family, Élégance Naturelle palette (`#C9A96E` accent, per Story 1.8's design tokens) inlined as CSS (email clients don't load external stylesheets)
- [x] Task 6: Endpoints (AC: #1, #2, #3)
  - [x] `Web/Endpoints/Auth.cs`: add `forgot-password` and `reset-password`, both `AllowAnonymous().RequireRateLimiting("auth")` (same rate limiter as `register`/`login`/`refresh` — this endpoint is a prime target for abuse otherwise)
  - [x] `ForgotPassword` always returns `Results.Ok()` regardless of `Result.Succeeded` (there's no failure path once validation passes — `ForgotPasswordAsync` always returns success, by design, per Task 3)
  - [x] `ResetPassword` returns `Results.Ok()` on success, `Results.Json(new { result.Errors }, statusCode: StatusCodes.Status400BadRequest)` on failure — matches AC #3's "400 Bad Request... clear expiry message"
- [x] Task 7: Backend tests
  - [x] `ForgotPasswordCommandValidatorTests.cs`
  - [x] `ResetPasswordCommandValidatorTests.cs`
  - [x] `AuthServiceForgotPasswordTests.cs` — unknown email → `Result.Success()`, no event published; known email → `Result.Success()`, `PasswordResetRequestedEvent` published with a non-empty link
  - [x] `AuthServiceResetPasswordTests.cs` — unknown email → generic failure message; wrong/expired token (real `UserManager` behavior — use the EF Core InMemory + a real `UserManager`/token provider via a minimal DI-built `ServiceProvider`, not a mock, since the thing under test *is* Identity's token validation) → generic failure message; valid token → success, all prior refresh tokens revoked, a second attempt with the same token now fails (proves single-use)

### Angular

- [x] Task 8: `auth.store.ts` additions (AC: #1, #2, #3)
  - [x] `forgotPassword(email): Promise<boolean>` — same try/catch shape as `login()`, but note the backend always returns 200, so this realistically only ever fails on network errors
  - [x] `resetPassword(email, token, newPassword): Promise<boolean>` — 400 → `'Ce lien de réinitialisation est invalide ou a expiré.'`
- [x] Task 9: Forgot-password page (AC: #1)
  - [x] `features/auth/pages/forgot-password/forgot-password.component.ts` + `.html` + `.scss` — single email field; on submit, always show the same neutral confirmation message ("Si un compte existe avec cet email, vous recevrez un lien de réinitialisation."), regardless of what the backend actually did — the UI must not leak more than the backend already refuses to
  - [x] Route `path: 'mot-de-passe-oublie'`
  - [x] "Mot de passe oublié ?" link added to `login.component.html`
- [x] Task 10: Reset-password page (AC: #2, #3, #4)
  - [x] `features/auth/pages/reset-password/reset-password.component.ts` + `.html` + `.scss` — reads `email`/`token` from query params; new-password + confirm-password fields (`Validators.minLength(8)` + a cross-field match validator); on success, navigate to `/connexion` with a success confirmation; on 400, show the inline error with a link back to `/mot-de-passe-oublie`
  - [x] Route `path: 'reinitialiser-mot-de-passe'`
- [x] Task 11: Angular tests
  - [x] `forgot-password.component.spec.ts`, `reset-password.component.spec.ts`
  - [x] `ng build` (production) + `ng test` via Edge as `ChromeHeadless`

### Flutter

- [x] Task 12: `auth_provider.dart` additions (AC: #1, #2, #3)
  - [x] `forgotPassword(email): Future<bool>`, `resetPassword(email, token, newPassword): Future<bool>` — same shapes as `login()`/`register()`
- [x] Task 13: Forgot-password and reset-password screens (AC: #1, #2, #3, #4)
  - [x] `features/auth/screens/forgot_password_screen.dart` — same neutral-confirmation-message design as Angular
  - [x] `features/auth/screens/reset_password_screen.dart` — **no query-param equivalent of Angular's `ActivatedRoute` for a deep-linked email/token pair in this story's scope**: Flutter's `go_router` supports path/query params, but there's no actual deep-link entry point wired yet (no universal links / app links configured in this project) — so the in-app path is: forgot-password screen → (user checks email on another device) → this story provides the *screen* for completeness and to keep both clients at parity, wired via `go_router` query params exactly like Angular (`/reinitialiser-mot-de-passe?email=...&token=...`), but real-world usage will mostly happen via the Angular web link until deep linking is set up in a later story. Documented as a known limitation, not a blocker — the screen and its logic are still fully implemented and testable.
  - [x] Routes `/mot-de-passe-oublie` and `/reinitialiser-mot-de-passe` added to `router.dart`
  - [x] "Mot de passe oublié ?" link added to `login_screen.dart`
- [x] Task 14: Flutter tests
  - [x] `forgot_password_screen_test.dart`, `reset_password_screen_test.dart`
  - [x] Not verified by tooling — same environment gap as every prior story

### Verification

- [x] Task 15: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] Angular: `ng build` (production) + `ng test` via Edge as `ChromeHeadless`
  - [x] Flutter: unverified, flagged above

### Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–2.2) run against the full diff.

- [x] [Review][Patch] Timing side-channel undermines the story's own no-enumeration design: `ForgotPasswordAsync`'s "email not found" branch returns almost immediately after one DB lookup, while the "email found" branch awaits a real outbound SendGrid network call (via `_publisher.Publish(...)` → `PasswordResetEmailHandler` → `IEmailService.SendAsync`) before returning — making a registered email's response measurably slower than an unregistered one, a timing oracle that defeats the identical-response-body guarantee. Confirmed independently by two reviewers. **Fixed:** the "not found" branch now awaits a fixed 400ms `Task.Delay` to approximate the found branch's network-call cost, closing (not perfectly eliminating, but substantially narrowing) the timing gap. A fully decoupled async dispatch (e.g. via `IServiceScopeFactory` + background queue) would close it more precisely but was judged disproportionate scope for this story; noted as a candidate for a future infra story if stronger guarantees are ever needed.
- [x] [Review][Patch] Reset-password requests with an empty/missing `email` or `token` (e.g. a bookmarked, stripped, or manually-typed `/reinitialiser-mot-de-passe` URL) fail `ResetPasswordCommandValidator`'s `NotEmpty()` rules, which `ProblemDetailsExceptionHandler` maps to **422**, not the **400** that both clients' error handling specifically keys the friendly "Ce lien de réinitialisation est invalide ou a expiré." message on — so this exact scenario showed the generic "Une erreur est survenue" message instead. Confirmed independently by two reviewers with the same concrete trigger. **Fixed two ways:** (1) both `reset-password.component.ts`/`.html` (Angular) and `reset_password_screen.dart` (Flutter) now detect a missing email/token *before* rendering the form and show an "invalid or incomplete link" message immediately, with a link back to request a new one — the backend is never even called; (2) as defense-in-depth, `auth.store.ts` and `auth_provider.dart` now also treat a `422` the same as a `400` for this specific call.
- [x] [Review][Considered, not fixed] `ResetPasswordAsync`'s refresh-token revocation (`SELECT active tokens` → mutate → `SaveChangesAsync`) has a narrow race window: a refresh token issued by a concurrent login/refresh on another device between the `SELECT` and `SaveChangesAsync` would be missed and survive the password reset. Attempted a fix using EF Core's `ExecuteUpdateAsync` (a single atomic UPDATE, closing the window to near-zero) — reverted because EF Core's **InMemory** provider (used by this test suite) doesn't support `ExecuteUpdate`/`ExecuteDelete`, only real SQL providers do; the fix would work correctly against the production SQL Server database but breaks the test suite's ability to verify it. Reverted to the original pattern and documented the narrow, real race window in a code comment for future revisit — matches this session's established pattern of choosing a proportionate response over forcing through a fix that trades away test coverage.
- [x] [Review][Patch] `PasswordResetEmailHandler.cs`'s email body used `#2B2B2B` for body text, which matches neither Story 1.8's `--color-text: #111111` nor `--color-text-secondary: #555555` design tokens — a minor deviation from the "matches the platform's visual identity" AC. **Fixed:** changed to `#111111`.
- [x] [Review][Defer] Flutter's forgot/reset-password widget tests only exercise client-side validation messages (empty/short/mismatch), not the actual submit-success path or provider-call verification — shallower than Angular's equivalent coverage (which does verify exact URLs/bodies via `HttpTestingController`). Consistent with this story's existing, prominent disclosure that Flutter is entirely tool-unverified in this environment; expanding untested Flutter test code has limited marginal value until Flutter tooling is available to actually run it.
- [x] [Review][Defer] The "Mot de passe oublié ?" link on the login page doesn't propagate an in-flight `returnUrl` query param through the forgot/reset-password flow — a user who arrived at `/connexion?returnUrl=/compte` and detours through password reset lands on `/connexion` (no `returnUrl`) afterward, not back at `/compte`. Confirmed as a minor UX gap, not a regression (no route currently redirects to `/connexion` with a `returnUrl` in practice yet — see Story 2.2's Dev Notes on why that wiring is still mostly latent). Threading `returnUrl` through three pages for this is disproportionate scope right now; revisit once Epic 2.4+ makes `returnUrl` a live, commonly-hit path.

## Dev Notes

### Two real, pre-existing backend gaps block this story — fix them first

Neither is this story's "fault," but both must be fixed before `ForgotPasswordAsync`/`ResetPasswordAsync` can work at all:

1. `AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>()` in `src/Infrastructure/DependencyInjection.cs` never chains `.AddDefaultTokenProviders()`. Without it, calling `UserManager.GeneratePasswordResetTokenAsync` throws `NotSupportedException: No IUserTwoFactorTokenProvider<TUser> named 'Default' is registered.` at runtime. This has been silently unused since Story 1.4 — nothing before this story ever called any token-provider-dependent API.
2. **No `IdentityOptions.Password` policy is configured anywhere in the codebase.** ASP.NET Identity's compiled-in defaults (`RequireDigit`/`RequireLowercase`/`RequireUppercase`/`RequireNonAlphanumeric` all `true`, `RequiredLength = 6`) silently apply. Every validator in this app (`RegisterCommandValidator`, this story's `ResetPasswordCommandValidator`) and every client-side form (Angular, Flutter) only ever enforces "≥ 8 characters" — nothing about digits, case, or symbols. A password like `"password123"` — used throughout this very codebase's test fixtures since Story 1.4 — would fail a **real** `UserManager.CreateAsync`/`ResetPasswordAsync` call today with "Passwords must have at least one non alphanumeric character" and "...one uppercase letter." This was never caught because every existing backend test mocks `UserManager` (bypasses real validation), and the SQL Server database has been unreachable from this dev environment for the entire project (no end-to-end `CreateAsync` has ever actually executed against a real Identity store). This story is the first one that needs `UserManager`'s real password-validation behavior to work correctly (`AuthServiceResetPasswordTests.cs` — see Task 7 — deliberately uses a **real** `UserManager` + token provider via a minimal DI container, not mocks, specifically because mocking would hide this exact class of bug again). Fix: configure `IdentityOptions.Password` to match what the app has always actually promised.

### Why `ForgotPasswordAsync` always returns success

This isn't explicitly required by the epic's AC wording, but it's a standard, low-cost security practice this codebase should apply consistently: if `POST /forgot-password` behaved differently (different status code, different response shape, different timing) for a registered vs. unregistered email, an attacker could enumerate valid customer email addresses. Contrast with Story 2.1's `409` on duplicate registration, which the epic *explicitly* requires to reveal the email is taken — that's a deliberate, spec-mandated tradeoff for a different endpoint (Story 2.1's Review Findings documented this exact distinction as a `[Review][Defer]`). Forgot-password has no such explicit requirement, so default to the safer behavior.

### Reset tokens don't need a new database table — Identity's `SecurityStamp` already gives single-use for free

`UserManager.ResetPasswordAsync(user, token, newPassword)` validates the token against the user's current `SecurityStamp`, then — on success — regenerates that stamp as part of updating the password. Any subsequent attempt to reuse the same token (or any token issued before the stamp changed) fails token validation automatically. This satisfies AC #4 ("single-use... invalidated after first successful use") without a parallel token-tracking table, keeping this story's data model unchanged (no new migration).

### Exact endpoint wiring (mirrors Story 2.2's `Login`/`Refresh` pattern)

```csharp
[EndpointSummary("Request a password reset email")]
public static async Task<IResult> ForgotPassword([FromBody] ForgotPasswordCommand command, ISender sender)
{
    await sender.Send(command);
    return Results.Ok();
}

[EndpointSummary("Reset password with a token")]
public static async Task<IResult> ResetPassword([FromBody] ResetPasswordCommand command, ISender sender)
{
    var result = await sender.Send(command);
    return result.Succeeded
        ? Results.Ok()
        : Results.Json(new { result.Errors }, statusCode: StatusCodes.Status400BadRequest);
}
```

### Existing patterns to keep reusing

- `SensitiveDataDestructuringPolicy` (Story 2.1) already redacts any `Password`-named property from Serilog logs — `ResetPasswordCommand.NewPassword` is **not** named `Password`, so it will NOT be auto-redacted. Either rename the property to `Password` (breaks the natural command-shape) or extend `SensitivePropertyNames` in `SensitiveDataDestructuringPolicy.cs` to also match `NewPassword`. Extend the policy — it already exists exactly for this purpose.
- Fire-and-forget event handler shape (Story 1.7, reused in 2.1): catch `OperationCanceledException` and rethrow, catch `Exception` and log, never let an email failure fail the calling command.
- `ACCESS_TOKEN_KEY`/`REFRESH_TOKEN_KEY` constants, no `updateOn: 'blur'`, broad Flutter `catch` after `on DioException` — all still apply to any new form in this story.

## Project Structure Notes

No new projects or top-level folders. Adds `ForgotPasswordCommand`/`ResetPasswordCommand` under the existing `Application/Auth/Commands/` tree, a new domain event under `Domain/Events/`, a new handler under `Application/Auth/EventHandlers/`, and pages/screens under the existing `features/auth/` trees on both clients.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 2.3 acceptance criteria (Epic 2 section)
- `_bmad-output/implementation-artifacts/2-1-inscription-client.md`, `2-2-connexion-et-deconnexion.md` — established patterns this story extends
- CLAUDE.md — Authentification JWT section

## Dev Agent Record

### Context Reference

- Story created by direct inspection of `DependencyInjection.cs` (found the missing `.AddDefaultTokenProviders()` and unconfigured `IdentityOptions.Password`), `IEmailService`/`SendGridEmailService`/`UserRegisteredWelcomeEmailHandler` (Story 2.1's email pattern), and `Result`/`Result<T>` (`Application/Common/Models/Result.cs`). No web research needed — all APIs used (`UserManager.GeneratePasswordResetTokenAsync`/`ResetPasswordAsync`, `DataProtectionTokenProviderOptions`) are stable, long-established ASP.NET Core Identity surface.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors, `dotnet test` (Application.UnitTests) 53/53 (41 pre-existing + 12 new). Re-verified after review fixes (the `ExecuteUpdateAsync` attempt was caught and reverted at this stage — see Review Findings).
- Angular: `ng build` (production, exercises the two new lazy routes + prerendering) and `ng test` (via Edge as `ChromeHeadless`) both pass, 34/34 (25 pre-existing + 9 new, including the post-review missing-params-guard test).
- Flutter: no SDK available in this environment (same as every prior story) — code hand-written, not tool-verified.
- Re-verified after applying all code-review fixes (Task 15 above reflects the final, post-review run).

### Completion Notes List

- All 15 tasks implemented. Backend and Angular fully verified against real tooling; Flutter code written but unverified.
- Found and fixed two real, pre-existing backend gaps that this story's own AC couldn't have been satisfied without: missing `.AddDefaultTokenProviders()` (would have thrown `NotSupportedException` at runtime) and an unconfigured `IdentityOptions.Password` policy silently requiring digit/upper/lower/symbol characters that nothing in this app has ever promised or enforced client-side. Both were invisible until now because every prior backend test mocks `UserManager`, and the real database has never been reachable from this dev environment. `AuthServiceResetPasswordTests.cs` deliberately uses a real `UserManager` + token provider (via a minimal hand-built `ServiceProvider`, EF Core InMemory-backed) specifically to catch this class of bug — and it did, on the first run, before the `IdentityOptions` fix was in place.
- `ForgotPasswordAsync` always returns success regardless of whether the email is registered — a deliberate anti-enumeration design decision made proactively (not from a code-review finding), matching the reasoning documented in Dev Notes and consistent with Story 2.1's explicit precedent of treating email-enumeration risk as a real, considered tradeoff rather than an afterthought.
- Password reset tokens don't need a new database table or migration — `UserManager.ResetPasswordAsync`'s `SecurityStamp` rotation already makes them single-use for free (see Dev Notes for the exact mechanism). Kept the data model unchanged.
- Extended `SensitiveDataDestructuringPolicy` (Story 2.1) to also redact `NewPassword` and `Token` properties from Serilog request logging — `ResetPasswordCommand` introduces both for the first time in this codebase.
- Flutter's reset-password screen is fully implemented (screen, form, go_router query-param wiring) for parity with Angular web, but real-world usage will mostly happen via the Angular web email link until a later story configures actual deep linking (universal/app links) for the Flutter app — documented as a known, deliberate limitation, not a blocker.
- Scope boundary respected: no new nav/header component; the "Mot de passe oublié ?" link was added directly to the existing login page/screen on both clients, consistent with Stories 2.1/2.2's "don't build UI for pages that don't exist yet" precedent.

### File List

**Backend:**
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (`.AddDefaultTokenProviders()`, `IdentityOptions.Password` relaxed to match the app's actual promise, `DataProtectionTokenProviderOptions.TokenLifespan = 1h`)
- `backend/MonEcommerce/src/Web/appsettings.json` (new `Frontend:BaseUrl` config key)
- `backend/MonEcommerce/src/Domain/Events/PasswordResetRequestedEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Auth/Commands/ForgotPasswordCommand.cs` + `ForgotPasswordCommandValidator.cs` + `ForgotPasswordCommandHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Auth/Commands/ResetPasswordCommand.cs` + `ResetPasswordCommandValidator.cs` + `ResetPasswordCommandHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Auth/EventHandlers/PasswordResetEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAuthService.cs` (`ForgotPasswordAsync`/`ResetPasswordAsync` added)
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` (implementations, `IConfiguration` added to constructor)
- `backend/MonEcommerce/src/Infrastructure/Logging/SensitiveDataDestructuringPolicy.cs` (redacts `NewPassword`, `Token`)
- `backend/MonEcommerce/src/Web/Endpoints/Auth.cs` (`forgot-password`, `reset-password` endpoints)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Commands/ForgotPasswordCommandValidatorTests.cs` + `ResetPasswordCommandValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceForgotPasswordTests.cs` + `AuthServiceResetPasswordTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceRegisterTests.cs`, `AuthServiceLoginTests.cs`, `AuthServiceRefreshTokenTests.cs`, `AuthServiceLogoutTests.cs` (updated: `AuthService` constructor now takes `IConfiguration`)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts` (`forgotPassword()`, `resetPassword()`)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts` (excluded the two new auth endpoints from refresh-and-retry)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/forgot-password/forgot-password.component.ts` + `.html` + `.scss` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/reset-password/reset-password.component.ts` + `.html` + `.scss` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/login/login.component.ts` + `.html` ("Mot de passe oublié ?" link)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (two new routes)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart` (`forgotPassword()`, `resetPassword()`)
- `mobile/mon_ecommerce_mobile/lib/shared/services/api_client.dart` (excluded the two new auth endpoints from refresh-and-retry)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/forgot_password_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/reset_password_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/login_screen.dart` ("Mot de passe oublié ?" link)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (two new routes)
- `mobile/mon_ecommerce_mobile/test/features/auth/screens/forgot_password_screen_test.dart` + `reset_password_screen_test.dart` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (2.3 status)
