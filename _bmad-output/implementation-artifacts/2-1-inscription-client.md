# Story 2.1: Inscription Client

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to create an account with my email and password,
so that I can access customer-only features (checkout, order history, returns).

## Acceptance Criteria

1. **Given** a visitor fills in name, email, and password (≥ 8 characters) on the registration form, **when** they submit, **then** `POST /api/v1/auth/register` returns `200` with `accessToken` and `refreshToken`, a welcome email is sent via SendGrid within 30 seconds, and the user is redirected (to `/` — see Dev Notes, `/catalogue` doesn't exist until Epic 3).
2. **Given** an email address already registered, **when** registration is attempted with that email, **then** a `409 Conflict` ProblemDetails response is returned with a clear message.
3. Password field validation runs onBlur (inline error under field in `#C0564A`, matching the `--color-error`/`AppTokens.errorColor` token from Story 1.8).
4. The password is never stored in plain text or returned in any API response.
5. The form works identically on Angular web and Flutter mobile.

## Tasks / Subtasks

### Backend

- [x] Task 1: Add `Name` field — currently missing entirely (AC: #1)
  - [x] `ApplicationUser : IdentityUser` → added `public string Name { get; set; } = string.Empty;`
  - [x] `RegisterCommand` → `Name` added as first param
  - [x] `RegisterCommandValidator` → `RuleFor(x => x.Name).NotEmpty();` added
  - [x] `IAuthService.RegisterAsync`/`AuthService.RegisterAsync` → `name` param added, sets `user.Name`
  - [x] `RegisterCommandHandler` → passes `request.Name` through
  - [x] EF Core migration `AddApplicationUserName` generated (`AddColumn<string> Name nvarchar(max) not null default ""` on `AspNetUsers`) — **not applied** (`dotnet ef database update`): this environment cannot reach the `DESKTOP-M36577B` SQL Server instance from `appsettings.json` (same environment gap as .NET/Flutter SDKs — see Completion Notes). Migration file is generated and reviewed; applying it requires the user's local SQL Server to be running.
- [x] Task 2: 409 Conflict on duplicate email (AC: #2)
  - [x] `ConflictException.cs` created, same shape as `NotFoundException.cs`
  - [x] `AuthService.RegisterAsync` pre-checks `FindByEmailAsync` before `CreateAsync`, throws `ConflictException`
  - [x] `ProblemDetailsExceptionHandler.cs` — `ConflictException` → 409 case added
- [x] Task 3: Welcome email via domain event (AC: #1)
  - [x] `UserRegisteredEvent.cs` created
  - [x] `UserRegisteredWelcomeEmailHandler.cs` created, same fire-and-forget shape as Story 1.7
  - [x] `IPublisher` injected into `AuthService`, `Publish(new UserRegisteredEvent(...))` called directly after `CreateAsync` succeeds — confirmed `ApplicationUser` is not a `BaseEntity`, direct publish used as planned
- [x] Task 4: Backend tests
  - [x] `UserRegisteredWelcomeEmailHandlerTests.cs` — happy-path + failure-swallowed (`void`, not `async Task`, per the Story 1.9 lesson)
  - [x] `RegisterCommandValidatorTests.cs` created (didn't exist) — 4 tests covering valid input, empty name, short password, invalid email
  - [x] Verified against the **real .NET 9 SDK** via `docker run mcr.microsoft.com/dotnet/sdk:9.0` (this machine still only has .NET 10 locally) — `dotnet build`: 0 warnings/0 errors; `dotnet test`: 21/21 passed (15 pre-existing + 6 new)

### Angular — first real feature in this app; routing/HTTP/state infra doesn't exist yet

- [x] Task 5: Bootstrap client infra (AC: #5)
  - [x] `apiUrl` added to both environment files
  - [x] `provideHttpClient(withFetch(), withInterceptors([authInterceptor]))` in `app.config.ts` — added `withFetch()` too (Angular's recommended Fetch-based HTTP backend for SSR compatibility, not in the original plan but a reasonable/idiomatic addition for an SSR app)
  - [x] `auth.interceptor.ts` created — **deviation**: added an `isPlatformBrowser` guard not in the original plan. This app is SSR — the interceptor also runs server-side during prerendering, where `localStorage` doesn't exist and would throw. Guarded with `inject(PLATFORM_ID)` + `isPlatformBrowser`.
  - [x] `@ngrx/signals@^19.2.1` installed — confirmed correct version landed (not the incompatible 21.x)
- [x] Task 6: Auth state (AC: #1, #5)
  - [x] `auth.store.ts` created — `signalStore` with `register()` method, `isLoading`/`error` state. Same SSR `isPlatformBrowser` guard applied before `localStorage` writes.
- [x] Task 7: Registration page (AC: #1, #3, #5)
  - [x] `register.component.ts`/`.html`/`.scss` created — Reactive Forms, `updateOn: 'blur'`, inline errors in `text-error`, `aria-describedby` wiring per the project's established accessibility pattern (UX-DR17)
  - [x] Route added to `app.routes.ts` (lazy-loaded)
  - [x] Redirects to `/` on success
  - [x] 409 shows "Un compte existe déjà avec cet email." inline
- [x] Task 8: Angular tests
  - [x] `register.component.spec.ts` — 4 tests: create, onBlur inline error, valid submit + navigate, 409 duplicate-email message (using `HttpClientTestingController`, not a mocked store)
  - [x] `ng build` (production, exercises `fileReplacements` + the new route) and `ng test` — both pass, 11/11 (7 pre-existing + 4 new)

### Flutter — first real feature in this app; routing/HTTP/state infra doesn't exist yet

- [x] Task 9: Bootstrap client infra (AC: #5)
  - [x] `flutter_riverpod`, `go_router`, `dio`, `flutter_secure_storage` added to `pubspec.yaml` at the researched versions
  - [x] `secure_storage.dart` — `SecureStorage` wrapper + `secureStorageProvider`
  - [x] `api_client.dart` — `Dio` + token-attach interceptor (no refresh-retry, per scope boundary) + `apiClientProvider`
  - [x] `router.dart` created with `/` (new placeholder home) and `/inscription` routes; `main.dart` rewritten to `MaterialApp.router` + `ProviderScope` — **the counter-demo (`MyHomePage`/`_MyHomePageState`) is fully removed**, as planned
- [x] Task 10: Auth state (AC: #1, #5)
  - [x] `auth_provider.dart` — **deviation from the plan's `AsyncNotifier` suggestion**: used a plain `Notifier<AuthState>` with an explicit `AuthState { isLoading, error }` class instead, mirroring the Angular store's shape exactly rather than Riverpod's `AsyncValue` machinery — simpler and lower-risk to get right without being able to run `flutter analyze` to catch subtle `AsyncNotifier` API misuse
- [x] Task 11: Registration screen (AC: #1, #3, #5)
  - [x] `register_screen.dart` — `ConsumerStatefulWidget`, `Form` + 3 `TextFormField`s, `AutovalidateMode.onUserInteraction`, error text in `AppTokens.errorColor`
  - [x] Success → `context.go('/')`
  - [x] 409 → inline error via `authState.error`
- [x] Task 12: Flutter tests
  - [x] `register_screen_test.dart` — 2 tests: empty-form validation errors, invalid-email error
  - [x] `widget_test.dart` **updated** (not in the original task list, but required) — the pre-existing counter-demo smoke test referenced widgets that no longer exist; replaced with a smoke test for the new home screen
  - [x] **Not verified by tooling** — Flutter/Dart SDK still unavailable in this environment (same gap as Stories 1.1/1.8/1.9). Code hand-written; Riverpod 3.x (`Notifier`/`NotifierProvider`), go_router 17.x (`GoRouter`/`MaterialApp.router`/`context.go`), and `flutter_secure_storage`/`dio` APIs cross-checked against stable, long-established signatures I'm confident in — but genuinely unverified end-to-end. Higher risk than Stories 1.8/1.9's Flutter work given the added Riverpod/go_router surface area.
- [x] Task 13: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — 0 warnings/errors, 21/21 tests
  - [x] Angular: `ng build` (production) + `ng test` — both pass, 11/11
  - [x] Flutter: unverified, flagged above and in Completion Notes
- [x] Task 14: Post-code-review re-verification (after applying all 9 patch items + the decision item)
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build MonEcommerce.sln`: 0 warnings/errors. Caught and fixed a genuine `CS8767` nullability mismatch in `SensitiveDataDestructuringPolicy.TryDestructure`'s `out` parameter against `IDestructuringPolicy`'s actual signature, and a missing `using Serilog.Core;` in its test file. `dotnet test` (Application.UnitTests): 25/25 (23 prior + 2 new `AuthServiceRegisterTests`)
  - [x] Angular: `ng build` (production, SSR) — both routes prerender cleanly; `ng test` via Edge-as-ChromeHeadless — 11/11
  - [x] Flutter: still unverified (no SDK in this environment) — email-regex and broadened-catch changes are hand-written, not tool-checked

### Review Findings

- [x] [Review][Decision] `RegisterCommand.Password` is logged in plaintext via `LoggingBehaviour.cs`'s `{@Request}` Serilog destructuring, which runs for every MediatR request — **pre-existing since Story 1.4** (not introduced by this story), but AC #4 ("password never stored in plain text or returned in any API response") is arguably violated in spirit by this live logging gap. Fixing it properly means changing shared cross-cutting infrastructure (`LoggingBehaviour.cs`) used by every command, not just `RegisterCommand` — a bigger, riskier change than this story's stated scope. Needs a decision: fix now (touches shared logging infra) or defer with elevated priority given the severity (plaintext passwords in logs, not a cosmetic issue)? — **Resolved: fix now.** Added `SensitiveDataDestructuringPolicy` (`src/Infrastructure/Logging/SensitiveDataDestructuringPolicy.cs`), a general Serilog `IDestructuringPolicy` that redacts any `Password`-named property before `{@Request}` destructuring — covers `RegisterCommand`, `LoginCommand`, and any future command with a `Password` field. Wired into `Program.cs`'s `UseSerilog(...)` chain via `.Destructure.With<SensitiveDataDestructuringPolicy>()`. Covered by `SensitiveDataDestructuringPolicyTests.cs` (2 tests).
- [x] [Review][Patch] TOCTOU race on duplicate-email registration — confirmed with concrete evidence (`RequireUniqueEmail` not set, no unique index on `NormalizedEmail`, only on `NormalizedUserName`): two concurrent registrations for the same email can both pass the `FindByEmailAsync` pre-check, and the losing `CreateAsync` call can throw a raw `DbUpdateException` (unique constraint violation) that falls through to a generic 500 instead of the intended 409 [backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs] — **Fixed:** wrapped `CreateAsync` in `try/catch (DbUpdateException)`, rethrown as `ConflictException`. Covered by `AuthServiceRegisterTests.ShouldThrowConflictExceptionWhenCreateAsyncRacesIntoADuplicateEmail`.
- [x] [Review][Patch] No route defined for `/` in Angular — `register.component.ts` redirects to `/` on success, but `app.routes.ts` only defines `path: 'inscription'`, so the "happy path" navigates to an undefined route (blank router-outlet) [frontend/mon-ecommerce-web/src/app/app.routes.ts] — **Fixed:** added a placeholder `HomeComponent` (`features/home/home.component.ts`) and a `path: ''` route entry.
- [x] [Review][Patch] `updateOn: 'blur'` on the Angular form can cause Enter-key submission to read stale field values — pressing Enter while a field is focused doesn't reliably fire `blur` first, so `form.getRawValue()` can reflect the pre-edit value. The template's error display already gates on `.touched` (which is set on blur independent of `updateOn`), so removing `updateOn: 'blur'` fixes the staleness bug while preserving the onBlur-error UX [frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.ts] — **Fixed:** removed `updateOn: 'blur'` from the form group config.
- [x] [Review][Patch] `RegisterCommandValidator`'s `Name` rule has no `MaximumLength` — accepts unbounded-length input [backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommandValidator.cs] — **Fixed:** added `.MaximumLength(100)`.
- [x] [Review][Patch] Flutter's email validator only checks `value.contains('@')`, accepting input the backend's `EmailAddress()` rule and Angular's `Validators.email` would reject (e.g. `"@"`, `"a@"`) — violates AC #5's "works identically on Angular web and Flutter mobile" [mobile/mon_ecommerce_mobile/lib/features/auth/screens/register_screen.dart] — **Fixed:** replaced with a `RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$')` check.
- [x] [Review][Patch] `'accessToken'` storage key is hardcoded independently in both `auth.interceptor.ts` and `auth.store.ts` — a rename/typo in one silently desyncs auth for every request [frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts, frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts] — **Fixed:** added `core/constants/storage-keys.ts` (`ACCESS_TOKEN_KEY`, `REFRESH_TOKEN_KEY`), both files now import from it.
- [x] [Review][Patch] Flutter `AuthNotifier.register` only catches `DioException` — if `SecureStorage.saveTokens` itself throws (platform channel unavailable), the exception propagates uncaught, leaving `state.isLoading` stuck `true` with no user-visible recovery [mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart] — **Fixed:** added a broader `catch (e)` after the `on DioException` clause.
- [x] [Review][Patch] Welcome email is published (and, since MediatR's default publisher awaits handlers, fully sent) *before* `IssueTokensAsync`'s `SaveChangesAsync` confirms the refresh token was persisted — if that later save fails, the user gets an error response despite already having received a welcome email for a registration that never completed [backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs] — **Fixed:** reordered so `IssueTokensAsync` runs before `_publisher.Publish(...)`.
- [x] [Review][Patch] No test exercises `AuthService.RegisterAsync`'s actual duplicate-email → `ConflictException` path or `ProblemDetailsExceptionHandler`'s new 409 mapping end-to-end — only the validator and the email handler are tested in isolation [backend/MonEcommerce/tests/] — **Fixed:** added `AuthServiceRegisterTests.cs` (2 tests: pre-check duplicate, and the `DbUpdateException` race path).
- [x] [Review][Defer] Email-enumeration via the 409 response ("Un compte existe déjà avec cet email.") — this is what AC #2 explicitly requires ("a 409 Conflict ProblemDetails response is returned with a clear message"); the enumeration tradeoff is spec-mandated, not a coding oversight. Revisit only if the product later decides to trade UX clarity for enumeration-resistance.
- [x] [Review][Defer] Flutter's `AutovalidateMode.onUserInteraction` (validates on every keystroke after first interaction) is a meaningfully different trigger than Angular's onBlur-only validation — the story's Dev Notes call this "the closest equivalent" but don't flag the timing difference explicitly. Not fixing (would require disproportionate custom `FocusNode` wiring in Flutter for a minor UX timing difference) — documenting the known platform difference here instead.
- [x] [Review][Defer] Flutter `auth_provider.dart`'s `response.data!` force-unwraps the Dio response body with no null check — low risk (a 200 response from this endpoint is never null in practice) but exactly the class of bug the story's own "never verified by tooling" disclosure warns about
- [x] [Review][Defer] `auth.store.ts` only tracks `isLoading`/`error`, no `isAuthenticated`/`user` state — reasonable to defer to Story 2.2 (Connexion), which needs to model session state more broadly than just the registration action

## Dev Notes

### This story bootstraps the ENTIRE client-side app architecture, not just a form

Confirmed via direct inspection: `frontend/mon-ecommerce-web/src/app/` has zero feature components/folders beyond `app.component.*` and an empty `app.routes.ts` (`export const routes: Routes = [];`). `mobile/mon_ecommerce_mobile/lib/` has only `main.dart` (still the stock Flutter counter-demo template, just re-themed in Story 1.8) and the `app/theme/` folder — no `features/`, no `app/router.dart`. **Neither app has HTTP client, routing, or state management wired yet.** This story is genuinely first-feature-in-app for both clients — the user explicitly confirmed building the full architecture.md-specified infra now (NgRx Signal Store, Riverpod, go_router, dio) rather than a simplified stopgap, since this establishes the pattern every later screen (Epic 2-8) will reuse.

### Backend: 3 real gaps found, not just "wire the existing endpoint"

Direct inspection of `RegisterCommand.cs`, `RegisterCommandHandler.cs`, `AuthService.cs` (Story 1.4) confirms:
1. **No `Name` field anywhere** — `RegisterCommand` is currently `(string Email, string Password)` only; `ApplicationUser : IdentityUser` has zero extra properties. Needs a new EF migration.
2. **No 409 handling** — duplicate email currently falls into `AuthService`'s generic `!result.Succeeded` branch, which the endpoint maps to `Results.BadRequest(new { result.Errors })` (400, not RFC7807 ProblemDetails).
3. **No domain event / welcome email** — grep for "welcome"/"UserRegistered" across the whole backend returns zero matches. `RegisterCommandHandler`/`AuthService.RegisterAsync` publishes nothing today.

None of this is a regression — Story 1.4 only ever promised JWT infrastructure (tokens, refresh rotation), not the full registration UX this story's AC requires.

### Exact `ConflictException` → 409 wiring (copy this pattern precisely)

`src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` currently maps `ValidationException`→422, `NotFoundException`→404, `UnauthorizedAccessException`→401, `ForbiddenAccessException`→403 via a switch expression on the caught `exception`. Add a new arm:
```csharp
ConflictException ce => (StatusCodes.Status409Conflict, new ProblemDetails
{
    Status = StatusCodes.Status409Conflict,
    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
    Title = "Conflict",
    Detail = ce.Message
}),
```
`ConflictException` needs a `using` alias only if there's a name collision (there isn't — no existing `ConflictException` anywhere), unlike `NotFoundException` which needed `AppNotFoundException` aliasing against a BCL-adjacent name collision risk.

### `UserRegisteredEvent` handler — copy Story 1.7's exact fire-and-forget shape

```csharp
namespace MonEcommerce.Application.Auth.EventHandlers;

public class UserRegisteredWelcomeEmailHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<UserRegisteredWelcomeEmailHandler> _logger;

    public UserRegisteredWelcomeEmailHandler(IEmailService emailService, ILogger<UserRegisteredWelcomeEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                notification.Email,
                "Bienvenue chez MonEcommerce",
                $"Bonjour {notification.Name}, bienvenue ! Votre compte a été créé avec succès.",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonEcommerce Domain Event: failed to send welcome email for user {UserId}", notification.UserId);
        }
    }
}
```
No manual DI registration needed — `RegisterServicesFromAssembly` in `Application/DependencyInjection.cs` auto-discovers `INotificationHandler<T>` implementations anywhere under `src/Application/`, confirmed working for all 4 Story 1.7 handlers.

### Why `IPublisher.Publish` directly, not `AddDomainEvent`

`BaseEntity.AddDomainEvent`/`DispatchDomainEventsInterceptor` (Stories 1.3/1.7) only works for entities that inherit `BaseEntity` and go through `ApplicationDbContext.SaveChangesAsync`. `ApplicationUser : IdentityUser` is ASP.NET Identity's own base class — it doesn't inherit `BaseEntity`, and `UserManager<ApplicationUser>.CreateAsync` manages its own persistence internally (not through `ApplicationDbContext.SaveChangesAsync` in a way the interceptor observes). Trying to force the `AddDomainEvent` pattern here would not work. Inject `IPublisher` (MediatR) directly into `AuthService` and call `Publish` explicitly after `CreateAsync` succeeds — simpler and correct for this specific case.

### `@ngrx/signals` — pin `^19.2.1`, not `@latest`

Verified via `npm view @ngrx/signals@21.1.1 peerDependencies` → `{ "@angular/core": "^21.0.0" }`. This project is Angular `^19.2.0` (confirmed in `package.json`). Installing `@ngrx/signals@latest` would pull v21.x and immediately break with a peer-dependency conflict. Use `@ngrx/signals@^19.2.1` (latest release actually compatible with Angular 19.x, confirmed via `npm view @ngrx/signals@19 version`).

### Redirect target: `/`, not `/catalogue`

AC #1 says "redirected to the catalogue," but `/catalogue` doesn't exist yet — Epic 3 builds it. Both clients should redirect/navigate to `/` (root) for now. Update this to `/catalogue` when Epic 3 lands; not a blocker for this story.

### Known pre-existing gap, explicitly NOT this story's job to fix: production CORS

`Program.cs`'s `UseCors(...AllowAnyOrigin()...)` is only registered inside the `if (app.Environment.IsDevelopment())` block — there is no CORS policy at all for production. This means a deployed Angular app (Vercel) calling the deployed API (Railway) cross-origin would currently get no CORS headers in production. Local dev testing is unaffected (dev CORS is wide open). This is a real gap but out of scope for a registration-form story — flag it in Completion Notes / `deferred-work.md` if not already tracked, don't fix it here.

### Angular dev API port

`launchSettings.json` dev profile uses `http://localhost:5287` (confirmed in Story 1.9 research) — use this for `environment.ts`'s `apiUrl`.

### Project Structure Notes

- Backend: new files in `src/Domain/Events/`, `src/Application/Auth/EventHandlers/`, `src/Application/Common/Exceptions/`; modified `ApplicationUser.cs`, `RegisterCommand.cs`, `RegisterCommandHandler.cs`, `RegisterCommandValidator.cs`, `IAuthService.cs`, `AuthService.cs`, `ProblemDetailsExceptionHandler.cs`, `Auth.cs` (only if the 409 mapping needs an endpoint-level tweak — likely not, since the exception handler middleware handles it globally once `AuthService` throws).
- Angular: new `src/app/core/interceptors/`, `src/app/features/auth/` (matches architecture.md's proposed tree exactly: `features/auth/ (pages/login · register · auth.store.ts)`).
- Flutter: new `lib/shared/services/`, `lib/app/router.dart`, `lib/features/auth/` (matches architecture.md's proposed tree: `features/auth/ (screens/login · register · providers/auth)`).
- Do not build login (2.2), password reset (2.3), profile (2.4), or order history (2.5) screens/endpoints in this story — those are separate Epic 2 stories. Build only what registration needs, even though the infra (interceptor, router, auth store/provider) will be reused by those later stories.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.1: Inscription Client] — acceptance criteria
- [Source: _bmad-output/planning-artifacts/architecture.md line 228, 237-241, 405-410, 518-528, 550-554] — NgRx Signal Store / Riverpod+go_router+dio decisions, proposed `features/auth/` and `core/`/`shared/` tree for both clients
- [Source: backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommand.cs, RegisterCommandHandler.cs, RegisterCommandValidator.cs] — current state, confirmed no `Name` field
- [Source: backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs, ApplicationUser.cs] — current registration logic, confirmed no event publishing, no duplicate-email discrimination
- [Source: backend/MonEcommerce/src/Web/Endpoints/Auth.cs] — current endpoint, confirmed `Results.BadRequest` for all failures
- [Source: backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs] — exact pattern to extend for 409
- [Source: backend/MonEcommerce/src/Domain/Events/, src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs] — Story 1.7 event/handler pattern being followed
- [Source: frontend/mon-ecommerce-web/src/app/ (directory listing), package.json, app.routes.ts] — confirmed pure scaffold, no routing/HTTP/state infra
- [Source: mobile/mon_ecommerce_mobile/lib/ (directory listing), pubspec.yaml] — confirmed pure scaffold, still the counter-demo template
- [Source: npm view @ngrx/signals@21.1.1 peerDependencies, @ngrx/signals@19 version — fetched 2026-07] — version compatibility check
- [Source: backend/MonEcommerce/src/Web/Properties/launchSettings.json] — dev port 5287 (per Story 1.9 research)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `dotnet ef migrations add` initially failed: EF design-time host building failed DI validation because `IEmailService` is only conditionally registered when `SendGrid:ApiKey` is configured (Story 1.6's credentials-optional pattern), and no `.env` exists in this environment. This is a **pre-existing gap that predates this story** — likely never surfaced before because no migration was generated since Story 1.6 introduced the conditional registration, and `dotnet run`/`dotnet ef` are the only paths that build the full DI container (unit tests use mocks, `dotnet build` is compile-only). Worked around locally with `SendGrid__ApiKey=dummy-key` env var for the migration commands only, not committed anywhere. **This is a real bug worth fixing separately**: `WebApplicationBuilder` enables `ValidateOnBuild=true` in Development by default, so `dotnet run` in Development would likely hit the same failure — defeating the intent of Story 1.6's "credentials-optional" design. See Completion Notes.
- `dotnet ef database update` failed: cannot reach `Server=DESKTOP-M36577B` (the SQL Server instance in `appsettings.json`) from this environment — same category of environment gap as the missing .NET 9/Flutter SDKs. Migration file is generated and reviewed; not applied to a live DB.
- Backend build/test verified against the **real .NET 9 SDK** via `docker run mcr.microsoft.com/dotnet/sdk:9.0` (this machine still only has .NET 10 locally, confirmed again) — 0 warnings/errors, 21/21 tests.
- Angular: `ng build` (production, exercising the `fileReplacements` + new lazy route) and `ng test` (via Edge as `ChromeHeadless`, no Chrome installed here) both pass, 11/11 tests.
- Flutter: no SDK available in this environment (same as Stories 1.1/1.8/1.9) — code hand-written, not tool-verified.

### Completion Notes List

- All 13 tasks implemented. Backend and Angular fully verified (real .NET 9 SDK via Docker, real `ng build`/`ng test`). Flutter code written but unverified by tooling — higher risk than prior Flutter stories given this one adds a much larger API surface (Riverpod `Notifier`/`NotifierProvider`, go_router `GoRouter`/`MaterialApp.router`/`context.go`, `flutter_secure_storage`, `dio` interceptors) versus Stories 1.8/1.9's smaller Flutter footprint (theme tokens, Sentry init).
- **New pre-existing bug found, not fixed (out of this story's scope)**: the backend's DI container likely can't build in `dotnet run`/Development mode without a SendGrid API key configured, because `IEmailService` is conditionally registered (Story 1.6) but `ValidateOnBuild=true` (ASP.NET Core's Development default) eagerly validates the *entire* service graph, including the now-5 `INotificationHandler<T>` implementations that depend on it. This directly undermines Story 1.6's "credentials-optional" intent. Recommend a follow-up story/task: either register a no-op/console-logging `IEmailService` fallback when no SendGrid key is present (most robust fix, preserves credentials-optional spirit), or set `ValidateOnBuild=false` explicitly (loses a safety net). Logged in `deferred-work.md`.
- EF migration for `ApplicationUser.Name` was generated and reviewed but **not applied** — this environment cannot reach the target SQL Server instance. User needs to run `dotnet ef database update --project src/Infrastructure --startup-project src/Web` themselves once their local SQL Server is confirmed running.
- Scope boundary respected throughout: no 401-refresh-and-retry logic in either client's interceptor (that's Story 2.2), no login/password-reset/profile/order-history screens built (Epic 2's other stories).
- Two real SSR-specific bugs caught before they shipped: the Angular auth interceptor and auth store both write to `localStorage`, which doesn't exist during server-side prerendering — both guarded with `isPlatformBrowser`. Verified via an actual `ng build` (which runs prerendering) succeeding.
- Redirect targets are `/` (not `/catalogue`) in both clients, since the catalogue doesn't exist until Epic 3 — documented in the story's own Dev Notes, implemented as specified.
- **Code review round**: 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found 1 decision-needed item (password logged in plaintext — user chose "fix now") and 9 patch items; all 10 are now fixed (see Review Findings above) and re-verified against real tooling (Docker .NET 9 SDK, `ng build`/`ng test` via Edge). The rebuild after fixes caught a genuine `CS8767` compiler error in the new `SensitiveDataDestructuringPolicy` (its `out` parameter's nullability didn't match `IDestructuringPolicy`'s actual interface signature) and a missing `using Serilog.Core;` in its test — both fixed. 4 further findings were reviewed and explicitly deferred with rationale (see Review Findings).

### File List

**Backend — code review fixes:**
- `backend/MonEcommerce/src/Infrastructure/Logging/SensitiveDataDestructuringPolicy.cs` (new — redacts `Password` properties from Serilog `{@Request}` logging)
- `backend/MonEcommerce/src/Web/Program.cs` (wired `.Destructure.With<SensitiveDataDestructuringPolicy>()`)
- `backend/MonEcommerce/src/Infrastructure/Infrastructure.csproj` (added `Serilog` package reference)
- `backend/MonEcommerce/Directory.Packages.props` (added `Serilog` version)
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` (TOCTOU fix via `DbUpdateException` catch; reordered token issuance before event publish)
- `backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommandValidator.cs` (`Name` `MaximumLength(100)`)
- `backend/MonEcommerce/tests/Application.UnitTests/Common/Behaviours/SensitiveDataDestructuringPolicyTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceRegisterTests.cs` (new)

**Angular — code review fixes:**
- `frontend/mon-ecommerce-web/src/app/core/constants/storage-keys.ts` (new — shared `ACCESS_TOKEN_KEY`/`REFRESH_TOKEN_KEY`)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts` (imports shared constant)
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts` (imports shared constants)
- `frontend/mon-ecommerce-web/src/app/features/home/home.component.ts` (new — fixes missing `/` route)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (added `path: ''` route)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.ts` (removed `updateOn: 'blur'`)

**Flutter — code review fixes:**
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/register_screen.dart` (tightened email regex)
- `mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart` (broadened catch to non-`DioException` failures)

**Backend:**
- `backend/MonEcommerce/src/Infrastructure/Identity/ApplicationUser.cs` (added `Name`)
- `backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommand.cs` (added `Name`)
- `backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommandHandler.cs` (passes `Name`)
- `backend/MonEcommerce/src/Application/Auth/Commands/RegisterCommandValidator.cs` (added `Name` rule)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAuthService.cs` (signature updated)
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` (name param, 409 pre-check, event publish)
- `backend/MonEcommerce/src/Application/Common/Exceptions/ConflictException.cs` (new)
- `backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (409 case added)
- `backend/MonEcommerce/src/Domain/Events/UserRegisteredEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Auth/EventHandlers/UserRegisteredWelcomeEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260719004000_AddApplicationUserName.cs` + `.Designer.cs` (new, not applied)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/EventHandlers/UserRegisteredWelcomeEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Commands/RegisterCommandValidatorTests.cs` (new)

**Angular:**
- `frontend/mon-ecommerce-web/src/environments/environment.ts`, `environment.production.ts` (added `apiUrl`)
- `frontend/mon-ecommerce-web/src/app/app.config.ts` (HttpClient + interceptor)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.ts` + `.html` + `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (route added)
- `frontend/mon-ecommerce-web/package.json` (added `@ngrx/signals`)

**Flutter:**
- `mobile/mon_ecommerce_mobile/pubspec.yaml` (added 4 dependencies)
- `mobile/mon_ecommerce_mobile/lib/shared/services/secure_storage.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/shared/services/api_client.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/register_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/main.dart` (rewritten — counter demo removed)
- `mobile/mon_ecommerce_mobile/test/widget_test.dart` (rewritten — old test referenced removed widgets)
- `mobile/mon_ecommerce_mobile/test/features/auth/screens/register_screen_test.dart` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (2.1 status)
