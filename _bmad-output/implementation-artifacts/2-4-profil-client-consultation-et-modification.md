# Story 2.4: Profil Client — Consultation & Modification

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to view and edit my personal information (name, address, email),
so that my details are always up to date for future orders.

## Acceptance Criteria

1. **Given** an authenticated customer, **when** `GET /api/v1/account/profile` is called, **then** name, email, and saved addresses are returned.
2. **Given** valid updated data, **when** `PATCH /api/v1/account/profile` is called, **then** the changes are persisted and the updated profile is returned.
3. Form validation runs onBlur with inline errors (`aria-describedby` linked to field).
4. A success snackbar/toast confirms the save ("Profil mis à jour").
5. Email change requires current password confirmation.
6. The screen is accessible on Angular web and Flutter mobile.

## Tasks / Subtasks

### Backend — genuinely new; also the first use of two already-built-but-unused pieces of infra

- [x] Task 1: `Account` feature scaffolding (AC: #1, #2)
  - [x] `Application/Account/Models/ProfileDto.cs`: `public record ProfileDto(string Name, string Email, List<AddressDto> Addresses);`
  - [x] `Application/Account/Models/AddressDto.cs`: `public record AddressDto(Guid Id, string Street, string City, string PostalCode, string Country);`
  - [x] `Application/Common/Interfaces/IAccountService.cs`: `GetProfileAsync(string userId, ...)`, `UpdateProfileAsync(string userId, string name, string email, string? currentPassword, ...)`
  - [x] `Infrastructure/Identity/AccountService.cs` implements it; registered `AddTransient<IAccountService, AccountService>()` in `DependencyInjection.cs`, matching `IAuthService`'s registration pattern
- [x] Task 2: `GetProfileQuery` (AC: #1) — **first Query in this codebase; everything so far has been Commands**
  - [x] `Application/Account/Queries/GetProfileQuery.cs`: `[Authorize] public record GetProfileQuery : IRequest<ProfileDto>;` — **first actual use of the `[Authorize]` attribute + `AuthorizationBehaviour` MediatR pipeline anywhere in this codebase** (built in Story 1.5, never exercised until now)
  - [x] Handler resolves the current user id via `IUser` (`Web/Services/CurrentUser.cs`, also built in Story 1.4/1.5 and never exercised by a command/query handler until now — only used implicitly by `AuthorizationBehaviour` itself), delegates to `IAccountService.GetProfileAsync`
- [x] Task 3: `UpdateProfileCommand` (AC: #2, #5)
  - [x] `Application/Account/Commands/UpdateProfileCommand.cs`: `[Authorize] public record UpdateProfileCommand(string Name, string Email, string? CurrentPassword) : IRequest<Result<ProfileDto>>;`
  - [x] Validator: `Name` `NotEmpty().MaximumLength(100)` (matches `RegisterCommandValidator`'s rule exactly); `Email` `NotEmpty().EmailAddress()`
  - [x] `AccountService.UpdateProfileAsync`: loads the user via `UserManager.FindByIdAsync(userId)`; if the new email differs from the current one (case-insensitive), requires `CurrentPassword` to be supplied and valid (`UserManager.CheckPasswordAsync`) before applying it, and rejects if the new email is already taken by a *different* account; uses `UserManager.SetEmailAsync`/`SetUserNameAsync` (handles normalization correctly, unlike hand-rolling `NormalizedEmail`) then `UserManager.UpdateAsync` to persist `Name`
  - [x] **Deliberately not in scope**: revoking existing refresh tokens on email change. Unlike Story 2.3's password reset (where the AC explicitly requires revoking every session), this AC says nothing about session invalidation on email change — adding it would be scope creep beyond what's asked.
- [x] Task 4: Endpoint (AC: #1, #2)
  - [x] `Web/Endpoints/Account.cs`, `RoutePrefix => "/api/v1/account"`: `GET profile` and `PATCH profile`, both `RequireAuthorization()` (ASP.NET's JWT bearer gate — belt-and-suspenders with the MediatR-level `[Authorize]` behavior, same layering `Logout` already uses)
  - [x] `UpdateProfile` returns `Results.Ok(result.Value)` on success, `Results.BadRequest(new { result.Errors })` on failure (mirrors `Register`'s existing pattern — a business-rule failure like "wrong current password," not a "the link is broken" 400-vs-422 case like Story 2.3's, so no special status-code handling needed)
- [x] Task 5: Backend tests
  - [x] `UpdateProfileCommandValidatorTests.cs`
  - [x] `AccountServiceTests.cs` — `GetProfileAsync` returns name/email/addresses; `UpdateProfileAsync` with same email succeeds without requiring a password; with a different email and no/wrong password fails with a clear message; with a different email and correct password succeeds and the email is actually changed; with a different email that's already taken by another account fails

### Angular — first protected route in this app; activates the `returnUrl` mechanism built in Story 2.2/2.3

- [x] Task 6: Auth guard (AC: #6)
  - [x] `core/guards/auth.guard.ts`: `CanActivateFn` reading `AuthStore.isAuthenticated()`; if `false`, redirects to `/connexion?returnUrl=<attempted url>` via `router.createUrlTree(...)` (a `UrlTree` return value, not an imperative `router.navigate()` call — the idiomatic Angular guard pattern)
- [x] Task 7: Toast/snackbar (AC: #4) — **doesn't exist anywhere in this app yet**
  - [x] `core/services/toast.service.ts`: `providedIn: 'root'`, a `message` signal + `show(text: string)` method that auto-clears after ~4s
  - [x] `core/components/toast/toast.component.ts`: reads the signal, renders a fixed-position, `role="status"` banner in `bg-success text-white` (Story 1.8's `--color-success` token) when a message is present
  - [x] Mounted once in `app.component.html`, alongside `<router-outlet />`
- [x] Task 8: **Clean up `app.component.html`/`.ts`/`.spec.ts`** — still the untouched Angular CLI scaffold ("Hello, mon-ecommerce-web" placeholder, Angular logo, doc links) sitting *above* `<router-outlet />` on every single page since Epic 1; never removed in any prior story. Touching this file anyway for the toast mount point — replacing the boilerplate now, not scope creep, just finishing what should've been cleaned up already.
- [x] Task 9: `account.store.ts` (AC: #1, #2, #4, #5)
  - [x] `features/account/account.store.ts`: `signalStore` with `profile`, `isLoading`, `error` state; `loadProfile()` (`GET`), `updateProfile(name, email, currentPassword)` (`PATCH`, calls `toastService.show('Profil mis à jour')` on success)
- [x] Task 10: Profile page (AC: #1, #2, #3, #5, #6)
  - [x] `features/account/pages/profile/profile.component.ts` + `.html` + `.scss` — loads the profile on init; `name`/`email` fields always visible, `currentPassword` field shown conditionally (`@if`) only once the email field's value differs from the loaded profile's email — matches AC #5's intent without demanding a password on every save
  - [x] Same onBlur-gated inline-error pattern as every prior form in this app (no `updateOn: 'blur'`, `.touched`-gated errors, `aria-describedby`)
  - [x] Route `path: 'compte'`, `canActivate: [authGuard]`
  - [x] `home.component.ts`/`.ts` template: add a "Mon compte" link next to the existing "Se déconnecter" button when authenticated
- [x] Task 11: Angular tests
  - [x] `auth.guard.spec.ts` — authenticated → allows; unauthenticated → redirects to `/connexion` with the right `returnUrl`
  - [x] `toast.component.spec.ts` or covered via `profile.component.spec.ts`
  - [x] `account.store.spec.ts` — `loadProfile()`, `updateProfile()` (with and without email change)
  - [x] `profile.component.spec.ts` — loads and displays profile, onBlur inline errors, conditional password field, successful update shows toast
  - [x] `ng build` (production) + `ng test` via Edge as `ChromeHeadless`

### Flutter — first protected route in this app

- [x] Task 12: Route guard (AC: #6)
  - [x] `router.dart`: `GoRouter`'s async `redirect` callback — reads `secureStorageProvider.accessToken` directly (not through Riverpod's `authProvider.isAuthenticated`, to avoid needing `refreshListenable` wiring for a single protected route) and redirects `/compte` to `/connexion?returnUrl=...` when no token is present
- [x] Task 13: `account_provider.dart` (AC: #1, #2, #4, #5)
  - [x] `features/account/providers/account_provider.dart`: `Notifier`-based, `profile`/`isLoading`/`error` state; `loadProfile()`, `updateProfile(name, email, currentPassword)` — on success, shows a `SnackBar` via a `GlobalKey<ScaffoldMessengerState>` (Flutter's built-in snackbar mechanism — no new component needed, unlike Angular which had nothing to reuse)
- [x] Task 14: Profile screen (AC: #1, #2, #3, #5, #6)
  - [x] `features/account/screens/profile_screen.dart` — same shape as `register_screen.dart`/`login_screen.dart` (`ConsumerStatefulWidget`, `Form`, `AutovalidateMode.onUserInteraction`), conditional password field when the email field differs from the loaded profile's email
  - [x] Route `/compte` added to `router.dart`
  - [x] Home screen: "Mon compte" button added next to "Se déconnecter"
- [x] Task 15: Flutter tests
  - [x] `profile_screen_test.dart`
  - [x] Not verified by tooling — same environment gap as every prior story

### Verification

- [x] Task 16: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] Angular: `ng build` (production) + `ng test` via Edge as `ChromeHeadless`
  - [x] Flutter: unverified, flagged above

### Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–2.3) run against the full diff.

- [x] [Review][Patch] Silent partial update on email change: `UserManager.SetEmailAsync`/`SetUserNameAsync`'s `IdentityResult`s were discarded, and each call persists to the database immediately (via Identity's internal `UpdateUserAsync`). If the trailing, separate `UpdateAsync(user)` call (which only existed to persist `Name`) then failed, the email/username had already been committed while the overall operation still reported failure — the user's password was consumed for a change that appeared to fail, and a subsequent login with the *old* email would silently no longer work. **Fixed:** `user.Name` is now set on the tracked entity *before* the email-change branch runs, so the first successful `Set*Async` call's internal save persists both fields together; each `Set*Async` result is now checked and its errors surfaced instead of being discarded; the no-email-change path still does its own explicit `UpdateAsync`.
- [x] [Review][Note] The email-uniqueness check (`FindByEmailAsync` then later `SetEmailAsync`) has a narrow TOCTOU window between two different accounts racing for the same new email — same class of gap as Story 2.1's already-documented finding (no unique index on `NormalizedEmail`, `RequireUniqueEmail` not set). Not re-solved here; the root cause is a schema-level fix that would need its own migration, out of proportion for this story. Documented in a code comment referencing the Story 2.1 precedent.
- [x] [Review][Patch] `AccountService.UpdateProfileAsync` never trimmed `name` before persisting — `"  Alice  "` passed validation (`NotEmpty()` correctly rejects whitespace-only, but not padding) and was stored with the padding intact. **Fixed:** `name.Trim()` before use.
- [x] [Review][Patch] Race in `profile.component.ts`: the form rendered immediately while `loadProfile()` was still in flight (only the submit button was disabled). Typing during that window got silently clobbered when `patchValue()` fired on load completion, and `isEmailChanged` — compared against a still-empty `loadedEmail` — could spuriously show the current-password field before the real email loaded. A failed load also left `loadedEmail` permanently `''`, making any subsequently-typed email look "changed." Flutter's `profile_screen.dart` already avoided this via a `!_initialized` gate. **Fixed:** added an `initialized` signal that hides the entire form (showing a loading/error message instead) until the profile has actually loaded, matching Flutter's existing pattern.
- [x] [Review][Patch] `auth.guard.ts`/`AuthStore.isAuthenticated` staleness across browser tabs: logging out in one tab left `isAuthenticated` still `true` in every other open tab's in-memory signal (no `storage` event listener), so `authGuard` could momentarily grant navigation to `/compte` in a stale tab. No data actually leaked (the subsequent API call still 401s), but the guard's decision was wrong at that instant. **Fixed:** `auth.store.ts` now listens for the `storage` event and syncs `isAuthenticated` when `ACCESS_TOKEN_KEY` changes in another tab.
- [x] [Review][Patch] Flutter `router.dart`'s `redirect` callback awaited `secureStorageProvider.accessToken` with no error handling — `flutter_secure_storage` can throw on some Android devices (e.g. keystore not yet unlocked right after install/reboot), which would have propagated out of `redirect` and left navigation in a broken state instead of gracefully falling back to `/connexion`. **Fixed:** wrapped in try/catch, failing closed to "not authenticated" on any read error.
- [x] [Review][Defer] Flutter has no equivalent of Angular's "redirect to `/connexion` on refresh failure": if a user's session expires while already on the (now-existing) `/compte` screen, `api_client.dart`'s interceptor clears secure storage but never navigates anywhere, and go_router's `redirect` only re-evaluates on navigation attempts — the user stays on a "protected" screen showing a generic error until they manually navigate away and back. This is a direct consequence of the scope trim Story 2.2 already made deliberately ("no protected screens exist yet to redirect from") — now that one exists, this is worth reconsidering, but building the `NavigatorKey`/`refreshListenable` infrastructure properly is a larger piece of work than this review round; revisit if more protected Flutter screens are added.
- [x] [Review][Test-gap, fixed] No test exercised `[Authorize]`/`AuthorizationBehaviour` through the actual MediatR pipeline for the new `GetProfileQuery`/`UpdateProfileCommand` — `AccountServiceTests` calls `AccountService` directly, bypassing the pipeline entirely, so the "genuinely protected" claim in AC #1 had zero automated coverage of the enforcement mechanism itself. **Fixed:** added `AuthorizationPipelineTests.cs`, which builds the real MediatR pipeline via `AddApplicationServices()` (the exact registration `Program.cs` uses) with a stubbed `IUser`, and proves `UnauthorizedAccessException` is thrown when unauthenticated and the query succeeds when authenticated.

## Dev Notes

### This story activates three pieces of infrastructure that were built but never actually used

1. **`[Authorize]` attribute + `AuthorizationBehaviour` MediatR pipeline behavior** (`Application/Common/Security/AuthorizeAttribute.cs`, `Application/Common/Behaviours/AuthorizationBehaviour.cs`) — built in Story 1.5, `grep`-confirmed zero usages anywhere in `Application/` until this story. It's a class-level attribute (`[AttributeUsage(AttributeTargets.Class, ...)]`), which works fine on a `record` (records compile to classes) — apply it directly above `GetProfileQuery`/`UpdateProfileCommand`.
2. **`IUser`/`CurrentUser`** (`Web/Services/CurrentUser.cs`) — reads `ClaimsPrincipal` via `IHttpContextAccessor`, already wired into DI and already consumed internally by `AuthorizationBehaviour`, but no command/query handler has ever injected `IUser` directly to get "the current user's id" until this story's handlers do exactly that.
3. **The `returnUrl` query-param mechanism** in `login.component.ts` (Story 2.2) and the equivalent Flutter groundwork — built and tested in isolation, but nothing ever actually redirected *to* `/connexion` with a `returnUrl` because no protected route existed. This story's `authGuard` is the first thing that does.

### First Query in this codebase — same shape as a Command, different folder

Every feature so far (`Auth/Commands/`) has been a Command. There's no established `Queries/` convention to deviate from — `GetProfileQuery` follows the exact same `record : IRequest<T>` + Handler shape, just returning data instead of causing a side effect, filed under `Application/Account/Queries/` instead of `Commands/`.

### No AutoMapper — manual DTO construction, matching every existing response shape

`grep` for `IMapper`/`CreateMap` across `Application/` returns nothing — despite `AutoMapper` being a referenced package, no part of this codebase has ever used it for DTO projection (`AuthResponse` etc. are all hand-constructed records). Don't introduce it here either; construct `ProfileDto` by hand in `AccountService`, consistent with the established pattern.

### Exact email-change validation logic

```csharp
var user = await _userManager.FindByIdAsync(userId)
    ?? throw new NotFoundException(nameof(ApplicationUser), userId);

var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
if (emailChanged)
{
    if (string.IsNullOrEmpty(currentPassword))
        return Result<ProfileDto>.Failure(["Le mot de passe actuel est requis pour changer d'email."]);

    if (!await _userManager.CheckPasswordAsync(user, currentPassword))
        return Result<ProfileDto>.Failure(["Mot de passe actuel incorrect."]);

    var existing = await _userManager.FindByEmailAsync(email);
    if (existing != null && existing.Id != user.Id)
        return Result<ProfileDto>.Failure(["Un compte existe déjà avec cet email."]);

    await _userManager.SetEmailAsync(user, email);
    await _userManager.SetUserNameAsync(user, email);
}

user.Name = name;
var result = await _userManager.UpdateAsync(user);
```

Using `SetEmailAsync`/`SetUserNameAsync` rather than hand-setting `user.Email`/`NormalizedEmail` avoids duplicating Identity's normalization logic (the same class of mistake Story 2.3 found and fixed elsewhere in this codebase — trust `UserManager`'s own APIs over reimplementing what they already do correctly).

### Angular guard pattern — return a `UrlTree`, don't imperatively navigate

```typescript
export const authGuard: CanActivateFn = (route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) return true;

  return router.createUrlTree(['/connexion'], { queryParams: { returnUrl: state.url } });
};
```

`AuthStore.isAuthenticated` is set *synchronously* at store construction (Story 2.2 — reads `localStorage` directly in the `withMethods` factory, before first render), so there's no async race to worry about here, unlike Flutter's cold-start check.

### Flutter guard — read secure storage directly, skip Riverpod wiring for one route

Flutter's `authProvider.isAuthenticated` is set *asynchronously* (`_checkAuthStatus()`, Story 2.2/2.3) and go_router's `redirect` isn't naturally reactive to Riverpod state changes without `refreshListenable` ChangeNotifier bridging. For a single protected route, reading `secureStorageProvider.accessToken` directly inside an async `redirect` callback is simpler and avoids that plumbing:

```dart
redirect: (context, state) async {
  if (state.matchedLocation != '/compte') return null;
  final container = ProviderScope.containerOf(context, listen: false);
  final token = await container.read(secureStorageProvider).accessToken;
  return token == null ? '/connexion?returnUrl=${Uri.encodeComponent(state.matchedLocation)}' : null;
},
```

If more protected routes are added later, revisit with a proper `refreshListenable`-based approach — one route doesn't justify that infrastructure yet.

### `app.component.html` cleanup is incidental, not scope creep

Found while locating where to mount the new toast component: `app.component.html`/`.ts`/`.spec.ts` are still the **unmodified Angular CLI scaffold** — the "Hello, mon-ecommerce-web" placeholder with the Angular logo and doc-link pills — sitting above `<router-outlet />` on every page, unnoticed since Epic 1. Since this story already needs to edit this exact file to mount `<app-toast />`, removing the leftover boilerplate at the same time is essentially free; leaving it would mean a toast component sits next to Angular's demo content in production.

## Project Structure Notes

New `Application/Account/` folder (Models, Queries, Commands) parallel to the existing `Application/Auth/`. New `features/account/` folder on Angular and Flutter, parallel to `features/auth/`. New `core/guards/` and `core/services/`/`core/components/toast/` on Angular.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 2.4 acceptance criteria (Epic 2 section)
- `_bmad-output/implementation-artifacts/2-2-connexion-et-deconnexion.md`, `2-3-reinitialisation-du-mot-de-passe.md` — established patterns this story extends and activates (auth store shape, `returnUrl` mechanism)
- CLAUDE.md — "Ajouter une fonctionnalité (pattern standard)" section

## Dev Agent Record

### Context Reference

- Story created by direct inspection of `AuthorizeAttribute.cs`, `AuthorizationBehaviour.cs`, `IUser.cs`/`CurrentUser.cs` (confirmed unused via grep), `Address.cs` (confirmed already exists from Story 1.3), and Story 2.2's `login.component.ts` (confirmed `returnUrl` handling exists but nothing redirects to it yet). No web research needed — no new libraries.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors, `dotnet test` (Application.UnitTests) 64/64 (53 pre-existing + 11 new, including the post-review `AuthorizationPipelineTests`). Caught and fixed two real `CS0104` ambiguous-reference compiler errors during this build (`NotFoundException` vs `Ardalis.GuardClauses.NotFoundException`; `AccountService` vs `Stripe.AccountService`) — both resolved with the same `using Alias = ...` pattern already established in this codebase (`AppIdentityService`, `AppNotFoundException`).
- Angular: `ng build` (production, exercises the new `/compte` route + prerendering) and `ng test` (via Edge as `ChromeHeadless`) both pass, 43/43 (33 pre-existing + 10 new, including the post-review cross-tab-sync tests).
- Flutter: no SDK available in this environment (same as every prior story) — code hand-written, not tool-verified.
- Re-verified after applying all code-review fixes (Task 16 above reflects the final, post-review run).

### Completion Notes List

- All 16 tasks implemented. Backend and Angular fully verified against real tooling; Flutter code written but unverified.
- This story activated three pieces of infrastructure that existed but had zero real usage until now: the `[Authorize]` attribute + `AuthorizationBehaviour` MediatR pipeline (Story 1.5), `IUser`/`CurrentUser` (Story 1.4/1.5), and the `returnUrl` query-param mechanism on Angular's login page (Story 2.2) — none of these were exercised by any prior story's actual request flow.
- Found and fixed a genuinely stale piece of the codebase while locating where to mount the new toast component: `app.component.html`/`.ts`/`.spec.ts` were still the unmodified Angular CLI scaffold ("Hello, mon-ecommerce-web" placeholder, Angular logo, doc-link pills) sitting above `<router-outlet />` on every page since Epic 1 — replaced with a minimal shell (`<router-outlet />` + `<app-toast />`).
- Extended Flutter's login screen to honor a `returnUrl` query param (previously always navigated to `/`) — Angular already had this from Story 2.2, but nothing ever redirected *to* `/connexion` with a `returnUrl` until this story's Flutter route guard made that path live for real; without this fix, a user redirected from `/compte` who then logs in would land on `/` instead of back at `/compte`.
- No AutoMapper introduced — `ProfileDto` is hand-constructed in `AccountService`, consistent with every other DTO in this codebase (confirmed via `grep` that `IMapper`/`CreateMap` are used nowhere despite the package being referenced).
- Deliberately did not revoke refresh tokens on email change (unlike Story 2.3's password reset, where the AC explicitly requires it) — this AC says nothing about session invalidation on email change, so adding it would be scope creep.
- Flutter's snackbar uses `ScaffoldMessenger.of(context)` directly at the call site in `profile_screen.dart`, not a `GlobalKey<ScaffoldMessengerState>` as originally sketched in this story's task list — simpler, avoids a global key for a single use site, and is the standard idiomatic approach when the triggering widget already has its own `Scaffold`/`context`.
- Flutter's route guard reads `secureStorageProvider` directly inside `GoRouter`'s async `redirect` rather than wiring a `refreshListenable` bridge to Riverpod's `authProvider` — reasonable for a single protected route; revisit with proper reactive wiring once more protected routes exist.

### File List

**Backend:**
- `backend/MonEcommerce/src/Application/Account/Models/ProfileDto.cs`, `AddressDto.cs` (new)
- `backend/MonEcommerce/src/Application/Account/Queries/GetProfileQuery.cs` + `GetProfileQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Account/Commands/UpdateProfileCommand.cs` + `UpdateProfileCommandValidator.cs` + `UpdateProfileCommandHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAccountService.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (registered `IAccountService`, added the `AppAccountService` alias)
- `backend/MonEcommerce/src/Infrastructure/Logging/SensitiveDataDestructuringPolicy.cs` (redacts `CurrentPassword`)
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Auth/Commands/UpdateProfileCommandValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceTests.cs` (new)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/core/guards/auth.guard.ts` + `auth.guard.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/components/toast/toast.component.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.component.ts` + `.html` + `.spec.ts` (rewritten — removed the Angular CLI scaffold, mounted the toast)
- `frontend/mon-ecommerce-web/src/app/features/account/account.store.ts` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.ts` + `.html` + `.scss` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (`/compte` route, guarded)
- `frontend/mon-ecommerce-web/src/app/features/home/home.component.ts` ("Mon compte" link)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/account/providers/account_provider.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/account/screens/profile_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (`/compte` route + guard, "Mon compte" link)
- `mobile/mon_ecommerce_mobile/lib/features/auth/screens/login_screen.dart` (honors `returnUrl`)
- `mobile/mon_ecommerce_mobile/test/features/account/screens/profile_screen_test.dart` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (2.4 status)
