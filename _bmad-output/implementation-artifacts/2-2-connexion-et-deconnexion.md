# Story 2.2: Connexion & Déconnexion

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to log in with my email and password and log out,
so that I can access my account securely from web and mobile.

## Acceptance Criteria

1. **Given** valid credentials, **when** `POST /api/v1/auth/login` is called, **then** `accessToken` (1h) and `refreshToken` (7d) are returned.
2. **Given** the user is logged in on Angular web, **when** they click "Se déconnecter", **then** the `refreshToken` is revoked in the database and local tokens are cleared.
3. **Given** an expired `accessToken`, **when** any authenticated API call is made, **then** the Angular `AuthInterceptor` / Dio interceptor automatically calls `/auth/refresh` and retries the request.
4. Tokens are stored in `localStorage` (Angular) and `flutter_secure_storage` (Flutter).
5. After login the user is redirected to the catalogue (or the page they came from) — since the catalogue doesn't exist until Epic 3, this means `/` or a `returnUrl` query param, matching Story 2.1's precedent for the register flow.
6. A `401` on refresh failure redirects to the login page without data loss.

## Tasks / Subtasks

### Backend — mostly already wired by Story 1.4, but unused/untested

- [x] Task 1: Verify existing Login/Refresh/Logout wiring (AC: #1, #6)
  - [x] Confirm `LoginCommand`/`LoginCommandHandler`/`LoginCommandValidator`, `RefreshTokenCommand`/`RefreshTokenCommandHandler`, `LogoutCommand`/`LogoutCommandHandler`, and `AuthService.LoginAsync`/`RefreshTokenAsync`/`LogoutAsync` all already exist and are correctly wired to `Web/Endpoints/Auth.cs` (they do — confirmed by direct inspection, see Dev Notes). No new command/handler files needed.
  - [x] Confirm access token lifetime is 1h and refresh token lifetime is 7d (`JwtService.cs`: `Jwt:AccessTokenExpirationMinutes` defaults to `60`; `AuthService.IssueTokensAsync`: `DateTimeOffset.UtcNow.AddDays(7)`) — matches AC #1 already, no change needed.
- [x] Task 2: Fix silent error-message drop on login/refresh failure (AC: #1)
  - [x] `Auth.cs`'s `Login` and `Refresh` endpoints currently do `result.Succeeded ? Results.Ok(...) : Results.Unauthorized()` — the `Result<AuthResponse>.Failure(...)` messages computed in `AuthService` (e.g. `"Email ou mot de passe incorrect."`) are silently discarded; the client gets a bare 401 with no body. Fix both to mirror `Register`'s existing pattern: `Results.Json(new { result.Errors }, statusCode: StatusCodes.Status401Unauthorized)`.
- [x] Task 3: Backend tests — currently **zero tests exist** for Login/Refresh/Logout (AC: #1, #2, #3, #6)
  - [x] `LoginCommandValidatorTests.cs` — empty/invalid email, empty password, valid input
  - [x] `AuthServiceLoginTests.cs` — unknown email → failure with message; wrong password → failure with message; valid credentials → success with tokens
  - [x] `AuthServiceRefreshTokenTests.cs` — valid unexpired token → new tokens issued AND old token's `RevokedAt` is set (rotation); expired token → failure; already-revoked token → failure; unknown token string → failure
  - [x] `AuthServiceLogoutTests.cs` — existing unrevoked token → `RevokedAt` set and persisted; already-revoked or unknown token → no-op, no exception

### Angular — extends Story 2.1's auth infra, does not rebuild it

- [x] Task 4: Extend `auth.store.ts` with session state (AC: #2, #4, #5, #6)
  - [x] Add `isAuthenticated` to `AuthState`, initialized from `isPlatformBrowser(platformId) && !!localStorage.getItem(ACCESS_TOKEN_KEY)` at store construction (so a page refresh while logged in doesn't flash "logged out")
  - [x] Add `login(email, password): Promise<boolean>` — mirrors `register()`'s shape exactly (same try/catch, same 401 message override), sets `isAuthenticated: true` on success
  - [x] `register()` also sets `isAuthenticated: true` on success (currently doesn't — Story 2.1 didn't need it, Story 2.2 does since the store now tracks session state)
  - [x] Add `logout(): Promise<void>` — reads the stored refresh token, `POST /api/v1/auth/logout` (best-effort: clears local tokens and sets `isAuthenticated: false` even if the network call fails, since the user's intent to log out locally must always succeed)
  - [x] Add an internal `refresh(): Promise<boolean>` — reads the stored refresh token, `POST /api/v1/auth/refresh`, updates stored tokens on success, clears tokens + `isAuthenticated: false` on failure. Deduplicates concurrent calls (a single in-flight `Promise` shared across simultaneous 401s) so N parallel failing requests don't trigger N refresh calls.
- [x] Task 5: 401 refresh-and-retry interceptor (AC: #3, #6)
  - [x] Extend `auth.interceptor.ts`: on a `401` response from any request **other than** `/auth/login`, `/auth/register`, `/auth/refresh` themselves (to prevent recursion), call `authStore.refresh()`; on success, retry the original request with the new access token; on failure, navigate to `/connexion?returnUrl=<current path>` and propagate the error
  - [x] SSR guard preserved (`isPlatformBrowser`) — refresh/redirect logic is browser-only, same as the existing token-attach logic
- [x] Task 6: Login page (AC: #1, #5)
  - [x] `features/auth/pages/login/login.component.ts` + `.html` + `.scss` — same Reactive Forms shape as `register.component.ts` (no `updateOn: 'blur'`, per Story 2.1's fix; `.touched`-gated inline errors; `aria-describedby`)
  - [x] Route `path: 'connexion'` added to `app.routes.ts`, lazy-loaded
  - [x] On success: navigate to `route.snapshot.queryParamMap.get('returnUrl') ?? '/'`
  - [x] On 401: inline error "Email ou mot de passe incorrect."
- [x] Task 7: Logout control (AC: #2)
  - [x] `home.component.ts` — placeholder home already exists (Story 2.1); extend it to show "Se déconnecter" when `authStore.isAuthenticated()`, and "Se connecter" / "Créer un compte" links when not. No new nav/header component — Epic 2's later stories (2.4+) will build real account UI.
- [x] Task 8: Angular tests
  - [x] `auth.store.ts` — new tests for `login()`, `logout()`, `refresh()` (dedup behavior, failure clears tokens)
  - [x] `login.component.spec.ts` — mirrors `register.component.spec.ts`: create, onBlur inline error, valid submit + navigate (default and `returnUrl`), 401 inline error
  - [x] `auth.interceptor.spec.ts` — 401-triggers-refresh-and-retry, refresh-failure-redirects, auth endpoints excluded from the retry logic
  - [x] `ng build` (production) + `ng test` via Edge as `ChromeHeadless`

### Flutter — extends Story 2.1's auth infra, does not rebuild it

- [x] Task 9: Extend `auth_provider.dart` with session state (AC: #2, #4)
  - [x] Add `isAuthenticated` to `AuthState`
  - [x] Add `login(email, password): Future<bool>` — mirrors `register()`'s try/catch shape (`on DioException` + broad `catch`, per Story 2.1's fix)
  - [x] Add `logout(): Future<void>` — best-effort `POST /auth/logout`, always clears `SecureStorage` regardless of network outcome
- [x] Task 10: 401 refresh-and-retry in `api_client.dart` (AC: #3)
  - [x] Extend the `Dio` interceptor's `onError` hook: on a 401 from any request other than `/auth/login`, `/auth/register`, `/auth/refresh`, call a refresh function, retry the original request with `dio.fetch(options)` on success; on failure clear `SecureStorage` and let the error propagate (no in-app navigation-on-401 — there are no protected screens yet in this app; documented as a deliberate scope trim, same class of decision as Story 2.1's Flutter deferrals)
- [x] Task 11: Login screen (AC: #1)
  - [x] `features/auth/screens/login_screen.dart` — same shape as `register_screen.dart` (`ConsumerStatefulWidget`, `Form`, `AutovalidateMode.onUserInteraction`)
  - [x] Route `/connexion` added to `router.dart`
  - [x] Success → `context.go('/')`; failure → inline error
- [x] Task 12: Logout control
  - [x] Home screen (`router.dart`'s `_HomeScreen`) extended with a conditional "Se déconnecter" button, same reasoning as the Angular home component
- [x] Task 13: Flutter tests
  - [x] `login_screen_test.dart` — mirrors `register_screen_test.dart`
  - [x] Not verified by tooling — same environment gap as every prior Flutter story

### Verification

- [x] Task 14: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] Angular: `ng build` (production) + `ng test` via Edge as `ChromeHeadless`
  - [x] Flutter: unverified, flagged above

### Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–2.1) run against the full diff.

- [x] [Review][Patch] Flutter `api_client.dart`'s `onError` interceptor could recurse indefinitely: `dio.fetch(retriedOptions)` re-enters the same interceptor chain, and if the retried request also 401s (e.g. a permission-based 401 unrelated to token expiry), `onError` fires again with no marker that this request already went through one refresh-and-retry cycle — infinite loop of `/auth/refresh` calls. Confirmed independently by both Blind Hunter and Edge Case Hunter. **Fixed:** added an `extra['authInterceptorRetried']` flag set on the retried request before re-fetching; `onError` now short-circuits (propagates the error) instead of refreshing again if the flag is already set.
- [x] [Review][Patch] Neither client excluded `/auth/logout` from the 401-refresh-and-retry trigger list — clicking "Se déconnecter" with an already-expired access token would run a full refresh cycle first (transiently flipping `isAuthenticated` back to `true`), and on Angular could even redirect to `/connexion` mid-logout if the refresh also failed, even though `logout()`/`AuthNotifier.logout()` already treat any failure there as best-effort and clear local state regardless [frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts, mobile/mon_ecommerce_mobile/lib/shared/services/api_client.dart] — **Fixed:** added `/api/v1/auth/logout` to both exclusion lists. Covered by a new interceptor spec test.
- [x] [Review][Patch] SSR hydration mismatch on Angular's home page: the server never has a token, so it always renders the logged-out branch, but the client's very first render (before hydration settles) used the store's real `isAuthenticated` value — an already-logged-in user reloading the page would hydrate onto structurally different markup than the server sent [frontend/mon-ecommerce-web/src/app/features/home/home.component.ts] — **Fixed:** gated the authenticated branch behind a `hydrated` signal flipped one tick after the client's first render via `afterNextRender` (a no-op during SSR), so the client's initial render always matches the server's, then swaps in the real state immediately after.
- [x] [Review][Patch] Flutter race: `AuthNotifier.build()`'s unawaited `_checkAuthStatus()` (reads secure storage, sets `isAuthenticated: true` if a stale token is present) could resolve concurrently with a `login()`/`register()` call that then fails — since `copyWith`'s `isAuthenticated` param falls back to the *current* state when omitted, the failure branches inherited the stale `true` instead of correctly reflecting the failed attempt [mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart] — **Fixed:** both failure branches in `login()` and `register()` now explicitly pass `isAuthenticated: false`.
- [x] [Review][Patch] `login.component.ts`'s `returnUrl` handling: `router.navigateByUrl()` resolves to `false` (not a rejection) for a malformed/unroutable `returnUrl`, silently stranding a successfully-logged-in user on the login page with no feedback [frontend/mon-ecommerce-web/src/app/features/auth/pages/login/login.component.ts] — **Fixed:** falls back to `navigateByUrl('/')` when the first navigation returns `false`.
- [x] [Review][Patch] Missing test coverage: no `AuthServiceRefreshTokenTests` case combined an expired *and* revoked token — **Fixed:** added `ShouldFailWhenTokenIsBothExpiredAndRevoked`.
- [x] [Review][Note] `Auth.cs`'s `Login`/`Refresh` endpoints now surface `result.Errors` in the 401 body (previously silently dropped). Today's messages are all generic/safe ("Email ou mot de passe incorrect.", etc.), but this opens a channel for a future, more specific error message to leak to an unauthenticated caller without anyone revisiting this endpoint. Not a current vulnerability — documented as a comment in `Auth.cs` for future maintainers, not changed further.
- [x] [Review][Defer] AC #6 ("a 401 on refresh failure redirects to the login page") is fully implemented and tested on Angular web, but Flutter's `api_client.dart` only clears `SecureStorage` on unrecoverable refresh failure — it doesn't navigate anywhere, since there are no protected screens in the Flutter app yet to redirect *from* (Stories 2.4/2.5 build those). Confirmed by the Acceptance Auditor as a defensible, honestly-disclosed scope trim rather than an oversight — revisit once Flutter has its first protected screen.
- [x] [Review][Defer] `LoginCommandValidatorTests.cs` has no whitespace-only or very-long-input cases — FluentValidation's `NotEmpty()` already rejects whitespace-only strings, so this would be a redundant test, not a coverage gap with real risk.

## Dev Notes

### Backend is mostly already built — Story 1.4 over-delivered here

Direct inspection of `src/Application/Auth/Commands/` confirms `LoginCommand.cs`, `LoginCommandHandler.cs`, `LoginCommandValidator.cs`, `RefreshTokenCommand.cs`, `RefreshTokenCommandHandler.cs`, `LogoutCommand.cs`, `LogoutCommandHandler.cs` all already exist, and `AuthService.cs` (`src/Infrastructure/Identity/`) already implements `LoginAsync`, `RefreshTokenAsync` (with rotation — the old token's `RevokedAt` is set before new tokens are issued), and `LogoutAsync`. `Web/Endpoints/Auth.cs` already routes all three (`login`, `refresh` are `AllowAnonymous().RequireRateLimiting("auth")`; `logout` is `RequireAuthorization()`). This is unlike Story 2.1, where `Register` had three real gaps (no `Name`, no 409, no welcome email) — here the *logic* is correct and complete, but:
1. **Zero tests exist** for any of it (`grep` for `Login`/`Refresh`/`Logout` under `tests/` returns nothing)
2. **The 401 error message is silently dropped** — `Login`/`Refresh` endpoints do `result.Succeeded ? Results.Ok(...) : Results.Unauthorized()`, discarding the `Result<AuthResponse>.Failure([...])` message that `AuthService` already computed (e.g. `"Email ou mot de passe incorrect."` for login, `"Refresh token invalide ou expiré."` for refresh)

So this story's backend work is almost entirely testing + one small endpoint fix, not new domain logic. Don't rebuild what's already correct.

### Exact fix for the dropped error message

`Web/Endpoints/Auth.cs`, mirror `Register`'s existing pattern:

```csharp
[EndpointSummary("Login")]
public static async Task<IResult> Login([FromBody] LoginCommand command, ISender sender)
{
    var result = await sender.Send(command);
    return result.Succeeded
        ? Results.Ok(result.Value)
        : Results.Json(new { result.Errors }, statusCode: StatusCodes.Status401Unauthorized);
}
```

Same change for `Refresh`. `Logout` needs no change (it's `IRequest` with no `Result`, endpoint just returns `Results.Ok()` after `await sender.Send(command)` — already correct, nothing to leak).

### Angular: `auth.store.ts` already has `register()` — extend it, don't replace it

Story 2.1 built `auth.store.ts` (`signalStore` with `isLoading`/`error` state, `register()` method) and `auth.interceptor.ts` (attaches `Authorization: Bearer <token>`, `isPlatformBrowser`-guarded for SSR). Story 2.1's own review findings explicitly flagged and deferred `isAuthenticated`/session state to this story: *"`auth.store.ts` only tracks `isLoading`/`error`, no `isAuthenticated`/`user` state — reasonable to defer to Story 2.2 (Connexion), which needs to model session state more broadly than just the registration action."* This story is that deferred work.

`register()`'s existing try/catch shape (see `auth.store.ts`) is the template for `login()` — same `patchState(store, { isLoading: true, error: null })` → `firstValueFrom(http.post(...))` → success/`HttpErrorResponse` 401 branch. Copy it exactly, just change the endpoint and the 401 message.

### The refresh-and-retry interceptor is the one genuinely new piece of client logic

Neither client has ever needed to retry a request before. Key correctness risks to design around:
- **Recursion**: the interceptor's own refresh call must not re-trigger itself. Exclude `/auth/login`, `/auth/register`, `/auth/refresh` by URL before doing 401-triggered refresh logic.
- **Thundering herd**: if 5 requests all get 401 simultaneously (e.g. a page that fires several API calls on load, all with the same expired token), each must not independently call `/auth/refresh` — that would race multiple rotations against the same refresh token, and per `AuthService.RefreshTokenAsync`'s rotation logic, only the *first* concurrent refresh call would succeed (it revokes the old token before issuing new ones) — the rest would fail with `"Refresh token invalide ou expiré."` even though the user's session is actually fine. Dedupe with a single shared in-flight `Promise` in `AuthStore.refresh()`.
- **SSR**: same `isPlatformBrowser` guard as the existing interceptor logic — refresh-and-retry is meaningless during server-side prerendering (no token to refresh).

### Flutter's interceptor has no navigation target yet — that's fine, don't build one prematurely

There is no protected route in this app yet (no account/profile/order-history screens exist until Stories 2.4/2.5). AC #6 ("a 401 on refresh failure redirects to the login page without data loss") is meaningfully an Angular-web concern right now — Angular has a router with a home page to redirect *from*. Flutter's `api_client.dart` interceptor should still clear `SecureStorage` on unrecoverable refresh failure (so stale tokens don't linger), but building a global `NavigatorKey`-based redirect-on-401 mechanism for zero currently-protected screens would be speculative infrastructure. Document this as a deliberate scope trim, matching the class of decision Story 2.1 made for several Flutter items (see that story's `[Review][Defer]` entries for precedent).

### Redirect target: `/` and `returnUrl`, not `/catalogue`

Same reasoning as Story 2.1: `/catalogue` doesn't exist until Epic 3. AC #5's "the page they came from" is implemented as a `returnUrl` query param that `login.component.ts` reads and defaults to `/` if absent — this doesn't require building a route-guard system (nothing currently redirects *to* `/connexion` with a `returnUrl`; that wiring happens naturally once Epic 2.4+ builds protected account pages and can pass `?returnUrl=` when redirecting an unauthenticated user). Building the guard itself now would be speculative for routes that don't exist yet.

### Existing patterns to keep reusing (do not deviate without a documented reason)

- No `updateOn: 'blur'` on Angular forms (Story 2.1 code-review fix — causes Enter-key staleness)
- `ACCESS_TOKEN_KEY`/`REFRESH_TOKEN_KEY` from `core/constants/storage-keys.ts` (Story 2.1 code-review fix — don't reintroduce hardcoded string literals)
- `SensitiveDataDestructuringPolicy` already redacts any `Password`-named property from Serilog logs — `LoginCommand.Password` is already covered, no new logging work needed
- Flutter: broad `catch (e)` after `on DioException` (Story 2.1 code-review fix — prevents `isLoading` sticking `true` on non-Dio failures)

## Project Structure Notes

No new projects, packages, or top-level folders. This story adds files under the existing `features/auth/` (Angular) and `features/auth/` (Flutter) trees established in Story 2.1, plus test files under the existing `tests/Application.UnitTests/Auth/` (backend) tree.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 2.2 acceptance criteria (Epic 2 section)
- `_bmad-output/implementation-artifacts/2-1-inscription-client.md` — established patterns this story extends (auth store shape, interceptor SSR guard, storage-key constants, Flutter catch-widening)
- CLAUDE.md — JWT auth section: "Refresh token rotation: l'ancien token est révoqué à chaque refresh" (already implemented, confirmed in Dev Notes above)

## Dev Agent Record

### Context Reference

- Story created by direct inspection of the existing backend Auth commands/handlers/service and Story 2.1's Angular/Flutter auth infra — no web research needed (no new libraries introduced; this story wires existing, already-installed dependencies).

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors, `dotnet test` (Application.UnitTests) 41/41 (25 pre-existing + 16 new, including the post-review combined expired+revoked test).
- Angular: `ng build` (production, exercises the `/connexion` route + prerendering) and `ng test` (via Edge as `ChromeHeadless`) both pass, 25/25 (11 pre-existing + 14 new, including the post-review logout-exclusion test).
- Flutter: no SDK available in this environment (same as every prior story) — code hand-written, not tool-verified.
- Re-verified after applying all code-review fixes (Task 14 above reflects the final, post-review run).

### Completion Notes List

- All 14 tasks implemented. Backend and Angular fully verified against real tooling; Flutter code written but unverified.
- Confirmed by direct inspection that Login/Refresh/Logout commands, handlers, validator, and `AuthService` methods already existed from Story 1.4 — this story's backend work was almost entirely closing gaps (dropped error message, zero test coverage), not new domain logic. See Dev Notes for the exact diff.
- Added `Microsoft.EntityFrameworkCore.InMemory` (test-only, `Application.UnitTests` project) to enable real `DbSet<RefreshToken>` querying (`FirstOrDefaultAsync`) in `AuthServiceRefreshTokenTests`/`AuthServiceLogoutTests` — plain `Moq`-mocked `DbSet<T>` can't support EF Core's async LINQ operators (`IAsyncQueryProvider`), and this is the first backend test suite that needed to *query* a `DbSet`, not just call `.Add()`.
- The refresh-and-retry interceptor is the one genuinely new piece of client logic in this story (neither client ever needed to retry a request before). Designed against two concrete failure modes: (1) recursion — a 401 from `/auth/refresh` itself must not re-trigger refresh; excluded by URL. (2) thundering herd — N simultaneous 401s must not each call `/auth/refresh` independently, since the backend's rotation logic revokes the old token on the *first* successful refresh, which would fail the rest even though the session is fine. Both Angular (`AuthStore.refresh()`) and Flutter (`api_client.dart`'s `refreshInFlight` future) dedupe via a single shared in-flight request.
- Flutter's `AuthNotifier.build()` fires an unawaited `_checkAuthStatus()` (reads `flutter_secure_storage` async, updates `isAuthenticated` shortly after) rather than blocking on it — `flutter_secure_storage` has no synchronous read, unlike Angular's `localStorage`. Wrapped in try/catch (fails open to "logged out") — this also prevents `MissingPluginException` from crashing widget tests, where the secure-storage platform channel isn't available.
- **Deliberate scope trim**: Flutter's `api_client.dart` interceptor clears `SecureStorage` on unrecoverable refresh failure but does not navigate anywhere (no global `NavigatorKey`-based redirect-on-401). There are no protected screens in the Flutter app yet (Stories 2.4/2.5 build those) — building that plumbing now would be speculative. AC #6's "redirects to the login page" is implemented on Angular web, which has both a router and a page to redirect from today.
- `returnUrl` query-param handling on the Angular login page doesn't require a route-guard system — nothing currently redirects *to* `/connexion` with a `returnUrl` (no protected routes exist yet). The wiring is forward-compatible: once Epic 2.4+ adds protected account pages, they can redirect unauthenticated users with `?returnUrl=`, and the login page already honors it.
- Scope boundary respected: no new nav/header component built for the logout control — added directly to the existing placeholder home component/screen on both clients, consistent with Story 2.1's "don't build UI for pages that don't exist yet" precedent.

### File List

**Backend:**
- `backend/MonEcommerce/src/Web/Endpoints/Auth.cs` (Login/Refresh now return the failure message instead of a bare 401)
- `backend/MonEcommerce/Directory.Packages.props` (added `Microsoft.EntityFrameworkCore.InMemory`, test-only)
- `backend/MonEcommerce/tests/Application.UnitTests/Application.UnitTests.csproj` (added the InMemory package reference)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Commands/LoginCommandValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceLoginTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceRefreshTokenTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Services/AuthServiceLogoutTests.cs` (new)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts` (added `isAuthenticated` state, `login()`, `logout()`, `refresh()` with dedup)
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.ts` (401 refresh-and-retry logic)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/auth.interceptor.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/login/login.component.ts` + `.html` + `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/login/login.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (`/connexion` route added)
- `frontend/mon-ecommerce-web/src/app/features/home/home.component.ts` (conditional login/logout links)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/auth/providers/auth_provider.dart` (added `isAuthenticated` state, `login()`, `logout()`, cold-start `_checkAuthStatus()`)
- `mobile/mon_ecommerce_mobile/lib/shared/services/api_client.dart` (401 refresh-and-retry interceptor)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/login_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (`/connexion` route, conditional logout button on home)
- `mobile/mon_ecommerce_mobile/test/features/auth/screens/login_screen_test.dart` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (2.2 status)
