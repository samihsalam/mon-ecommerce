# Story 8.3: Droit à l'Oubli & Suppression des Données

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to request deletion of my personal data,
so that I can exercise my GDPR right to erasure.

## Acceptance Criteria

1. Given an authenticated customer, when they submit the "Supprimer mon compte" form in their account settings, then `POST /api/v1/account/delete-request` creates a deletion request and a confirmation email is sent within 30 seconds.
2. Given an admin processes the deletion request within 30 days, when the deletion is executed, then personal data is anonymised in the database: name → "Utilisateur supprimé", email → irreversible hash, address → removed.
3. Given the account is anonymised, when the order history is checked, then order records are retained for accounting obligations but personal identifiers are removed.
4. The deletion request is logged with timestamp and processing admin.
5. After anonymisation the customer cannot log in with their old credentials.
6. Stripe customer data deletion is also requested via the Stripe API.

## Tasks / Subtasks

- [x] Task 1: Domain — `AccountDeletionRequest` entity + `AccountDeletionStatus` enum (AC #1, #2, #4)
  - [x] Subtask 1.1: `Domain/Entities/AccountDeletionRequest.cs` extends `BaseAuditableEntity` (gives `Created`/`LastModified` for free, same as `Return`) — `UserId` (string), `Status` (`AccountDeletionStatus`, default `Pending`), `ProcessedByAdminUserId` (`string?`), `ProcessedAt` (`DateTimeOffset?`). No `OriginalEmail` field — the request-confirmation email (AC #1) is sent synchronously at request time using the customer's *current, still-real* email via `IIdentityService.GetEmailAsync`, resolved inside the email handler like `ReturnRequestedEmailHandler` does — there is no need to freeze a copy of the email on the entity itself, and doing so would work against the "irreversible" spirit of AC #2 for the interim period before processing.
  - [x] Subtask 1.2: `Domain/Enums/AccountDeletionStatus.cs` — `Pending`, `Processed`. Two values only, same minimalism as `ReturnStatus`/`OrderStatus` — no "Rejected"/"Cancelled" state exists because no AC describes one; do not invent one.
  - [x] Subtask 1.3: `Domain/Events/AccountDeletionRequestedEvent.cs` — `record AccountDeletionRequestedEvent(Guid RequestId, string CustomerEmail) : BaseEvent`, same shape as `ReturnRequestedEvent`.
  - [x] Subtask 1.4: `Infrastructure/Data/Configurations/AccountDeletionRequestConfiguration.cs` (`IEntityTypeConfiguration<AccountDeletionRequest>`) — mirror `ReturnConfiguration`'s conventions (check that file for exact patterns: string length limits, enum-to-string-or-int convention already used for `Status`).
  - [x] Subtask 1.5: Add `DbSet<AccountDeletionRequest> AccountDeletionRequests` to `IApplicationDbContext` and `ApplicationDbContext`, alongside the existing `DbSet<Return> Returns`.
  - [x] Subtask 1.6: EF migration: from `backend/MonEcommerce/` run `dotnet ef migrations add AddAccountDeletionRequest --project src/Infrastructure --startup-project src/Web`, then `dotnet ef database update --project src/Infrastructure --startup-project src/Web` (SQL Server `DESKTOP-M36577B` — if unreachable from this environment, note it as deferred the same way Story 2.1's Dev Agent Record did, do not skip generating the migration file itself).

- [x] Task 2: Customer-facing request command (AC #1)
  - [x] Subtask 2.1: `Application/Account/Commands/RequestAccountDeletionCommand.cs` — `[Authorize] public record RequestAccountDeletionCommand : IRequest<Guid>` (no parameters — resolved via `IUser` inside the handler, same convention as `CreateReturnRequestCommand`). No validator needed (nothing to validate — no free-text input).
  - [x] Subtask 2.2: `RequestAccountDeletionCommandHandler` — creates the `AccountDeletionRequest` row (`UserId = _user.Id!`, `Status = Pending`), resolves the customer's current email via `IIdentityService.GetEmailAsync(_user.Id!)`, raises `AccountDeletionRequestedEvent(requestId, email)` on the new entity (same `AddDomainEvent` + `SaveChangesAsync` pattern as `CreateReturnRequestCommandHandler`), returns the new request's `Id`.
  - [x] Subtask 2.3: Idempotency guard — if the calling user already has a `Pending` `AccountDeletionRequest`, throw `ConflictException` ("Une demande de suppression est déjà en cours pour ce compte.") instead of creating a duplicate row. Query `_context.AccountDeletionRequests.AnyAsync(r => r.UserId == _user.Id! && r.Status == AccountDeletionStatus.Pending, ct)` before inserting.

- [x] Task 3: Email handler (AC #1)
  - [x] Subtask 3.1: `Application/Account/EventHandlers/AccountDeletionRequestedEmailHandler.cs` (`INotificationHandler<AccountDeletionRequestedEvent>`) — same shape as `ReturnRequestedEmailHandler`: build HTML via `EmailTemplateBuilder.Wrap`, call `IEmailService.SendAsync(notification.CustomerEmail, "Votre demande de suppression de compte a été reçue", htmlBody, "AccountDeletionRequested", ct)` inside a `try/catch` that logs and swallows `Exception` (but rethrows `OperationCanceledException`) — same non-blocking-on-email-failure convention as every other Epic 5 email handler. `SendGridEmailService`'s existing retry-up-to-3 + logging behavior (Story 5.4) already satisfies AC #1's "within 30 seconds" — no new timing logic needed in this handler.

- [x] Task 4: `IPaymentService.DeleteCustomerDataAsync` (AC #6)
  - [x] Subtask 4.1: Add `Task DeleteCustomerDataAsync(string email, CancellationToken ct = default)` to `Application/Common/Interfaces/IPaymentService.cs`. **Critical context**: this codebase's checkout flow (`CreatePaymentIntentCommandHandler`) never creates a Stripe `Customer` object — `StripePaymentService.CreatePaymentIntentAsync` has no `Customer` parameter, payments are anonymous `PaymentIntent`s only. So there is, today, no Stripe Customer to delete for any real order in this system. AC #6 still requires the *request* to be made — implement this as a defensive, best-effort lookup: search Stripe for a Customer matching the email (`CustomerService.ListAsync(new CustomerListOptions { Email = email, Limit = 1 }, cancellationToken: ct)`) and delete the first match if found (`CustomerService.DeleteAsync`); no-op (not an error) if none exists. Wrap the Stripe call in try/catch inside the *handler* (Task 5), not here — this method should propagate Stripe exceptions like `CreateRefundAsync` does, letting the caller decide how to handle failure.
  - [x] Subtask 4.2: `StripePaymentService` implements it, injecting a new `Stripe.CustomerService` (register `builder.Services.AddTransient<CustomerService>();` in `Infrastructure/DependencyInjection.cs` right next to the existing `PaymentIntentService`/`RefundService` registrations — same `AddTransient` pattern, same `if (stripeKey != null)` block).

- [x] Task 5: `IIdentityService.AnonymizeUserAsync` (AC #2, #5) — new Application-layer capability, NOT a direct `UserManager` call from the command handler
  - [x] Subtask 5.1: Add `Task<Result> AnonymizeUserAsync(string userId, string anonymizedName, string anonymizedEmail, CancellationToken ct = default)` to `IIdentityService` — the Application layer has no reference to ASP.NET Identity/`UserManager` (Clean Architecture boundary, see `CLAUDE.md`), so `ProcessAccountDeletionCommandHandler` (Task 6) cannot call `UserManager` directly; it must go through this new interface method, same as `DeleteUserAsync` already wraps `UserManager.DeleteAsync`.
  - [x] Subtask 5.2: `IdentityService.AnonymizeUserAsync` implementation — sets `user.Name = anonymizedName`, then `UserManager.SetEmailAsync` + `UserManager.SetUserNameAsync` (both must be updated together — ASP.NET Identity requires `UserName`/`Email` to stay in sync, same pattern `AccountService.UpdateProfileAsync`'s email-change path already follows), returns `Result.Failure` if either Identity call fails (propagate `IdentityResult.Errors` via the existing `ToApplicationResult()` extension, same as `DeleteUserAsync`).

- [x] Task 6: Admin-facing processing command + query (AC #2, #3, #4, #5, #6)
  - [x] Subtask 6.1: `Application/Account/Commands/ProcessAccountDeletionCommand.cs` — `[Authorize(Roles = Roles.Administrator)] public record ProcessAccountDeletionCommand(Guid RequestId) : IRequest`.
  - [x] Subtask 6.2: `ProcessAccountDeletionCommandHandler` — resolve the `AccountDeletionRequest` by id (`NotFoundException` if missing), throw `ConflictException` ("Cette demande a déjà été traitée.") if `Status != Pending` (same "already processed" guard shape as `UpdateReturnStatusCommandHandler`). Then, in order:
    1. Resolve the customer's current email via `IIdentityService.GetEmailAsync(request.UserId)` (needed before anonymizing it, for step 4 below).
    2. Compute the irreversible hash: `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalEmail + accountDeletionRequest.UserId)))[..32].ToLowerInvariant() + "@deleted.invalid"` — the `.invalid` TLD is reserved by RFC 2606 specifically for "not a real, deliverable address" use, and salting with `UserId` prevents two customers who once shared an email pattern from colliding.
    3. Call `IIdentityService.AnonymizeUserAsync(request.UserId, "Utilisateur supprimé", anonymizedEmail, ct)` (AC #2) — throw if `Result.Succeeded` is false (surfacing `IdentityResult` errors, same failure-propagation shape used elsewhere). This single step is *also* what satisfies AC #5 ("cannot log in with old credentials") — login resolves the account by email/username, so once the old email no longer maps to this account, the old credentials cannot authenticate, full stop; no separate "disable login" flag is needed or should be invented.
    4. Additionally revoke every active refresh token for the user (`_context.RefreshTokens.Where(t => t.UserId == request.UserId && t.RevokedAt == null)`, set `RevokedAt = _timeProvider.GetUtcNow()` on each) — AC #5 only literally requires blocking *new* logins, but a right-to-erasure request whose *purpose* is "stop this person's access to my account" should not leave an already-issued refresh token silently valid on another device; this is the same `TimeProvider`-based timestamping convention `CreateReturnRequestCommandHandler` uses.
    5. Hard-delete all `Address` rows for `request.UserId` (AC #2's "address → removed" — unlike name/email, addresses are deleted outright, not anonymized in place, since nothing downstream needs a placeholder address).
    6. Call `IPaymentService.DeleteCustomerDataAsync(originalEmail, ct)` (AC #6), wrapped in try/catch — log a warning and continue on failure (a Stripe-side failure must not block the anonymization already performed above; same non-blocking-on-external-failure philosophy as the email handlers, though here it's a warning log rather than silent swallow since this is an admin-triggered action, not a fire-and-forget domain event).
    7. Set `Status = Processed`, `ProcessedByAdminUserId = <the calling admin's user id, via IUser>`, `ProcessedAt = _timeProvider.GetUtcNow()` (AC #4 — this row IS the audit log; no separate audit table needed, same "the entity's own status/timestamp fields are the audit trail" convention as `Return`/`PaymentAuditLog`).
    8. `SaveChangesAsync`. **Do NOT delete the `ApplicationUser` row itself or the `Order` rows** — AC #3 explicitly requires order records to be retained (for accounting obligations) with only personal identifiers removed, which means `Order.UserId` must keep resolving to a real (now-anonymized) `ApplicationUser` row, not a dangling/deleted foreign key. `Order` already stores no separate customer name/email of its own (confirmed: `Order` entity has no `CustomerName`/`CustomerEmail` fields — it only references `ShippingAddress` and `UserId`), so anonymizing the `ApplicationUser` + deleting `Address` rows is sufficient to remove personal identifiers from the order's full picture; no `Order`-table changes are needed for AC #3.
  - [x] Subtask 6.3: `Application/Account/Queries/GetAccountDeletionRequestsQuery.cs` + handler — `[Authorize(Roles = Roles.Administrator)]`, returns pending `AccountDeletionRequest`s (id, `UserId`, `Created`) ordered oldest-first (so an admin naturally works the 30-day-window queue in FIFO order) — same shape/purpose as `GetAdminReturnsQuery`. This is the minimum needed for an admin to discover which requests exist to process (AC #2 presupposes an admin can find pending requests) — **no Angular admin UI is built for this** (see Dev Notes: this codebase's entire admin surface across Epics 6–7 is backend-API-only, confirmed via `frontend/mon-ecommerce-web/src/app/app.routes.ts` having zero `/admin` routes; there is no separate admin frontend project either). Admins operate this endpoint directly (Postman/`Web.http`/ops tooling), consistent with every prior "admin" story in this codebase.

- [x] Task 7: Endpoints (AC #1, #2)
  - [x] Subtask 7.1: In `Web/Endpoints/Account.cs`, add `groupBuilder.MapPost(RequestAccountDeletion, "delete-request").RequireAuthorization();` and a handler method `RequestAccountDeletion(ISender sender) => Results.Ok(new { requestId = await sender.Send(new RequestAccountDeletionCommand()) })` (method name must stay unique across all endpoint groups per this codebase's reflection-based discovery — `RequestAccountDeletion` doesn't collide with anything existing).
  - [x] Subtask 7.2: New `Web/Endpoints/AdminAccountDeletions.cs` (`IEndpointGroup`, `RoutePrefix => "/api/v1/admin/account-deletions"`) — `GET ""` → `GetAccountDeletionRequestsQuery`, `POST "{requestId:guid}/process"` → `ProcessAccountDeletionCommand`, both `.RequireAuthorization()` at the route level (the real Role gate is each command/query's own `[Authorize(Roles = Roles.Administrator)]`, enforced by `AuthorizationBehaviour` — same split as `AdminReturns.cs`).

- [x] Task 8: Frontend — "Supprimer mon compte" (AC #1)
  - [x] Subtask 8.1: `AccountStore` (`features/account/account.store.ts`) gains `async requestAccountDeletion(): Promise<boolean>` — `POST /api/v1/account/delete-request`, same `patchState`/try-catch/`ToastService` shape as `updateProfile`, plus a new `deletionRequested: boolean` field in `AccountState` (persists in the store, not just component-local state, so the confirmation banner survives if the user navigates away and back within the session).
  - [x] Subtask 8.2: `profile.component.html`/`.ts` — a new "Supprimer mon compte" section below the existing profile form: a button that reveals an inline confirmation panel (explanatory text: the account will be deleted within 30 days, this cannot be undone) with a "Confirmer la suppression" button calling `accountStore.requestAccountDeletion()`; and a "Annuler" button to collapse the panel without submitting. On success, replace the section with a persisted confirmation message and hide the delete button (`@if (accountStore.deletionRequested())`) — do NOT log the user out or disable the rest of the profile page: AC #2 says the admin processes the request "within 30 days", so the account remains fully usable until then; there is no AC requiring immediate session termination on request (only on *processing*, which is a backend-only admin action per Task 6).

### Review Findings

**Patches (fixed during review):**

- [x] [Review][Patch] **CRITICAL** — hard-deleting `Address` rows violates `Order.ShippingAddressId`'s `OnDelete(DeleteBehavior.Restrict)` FK, throwing on `SaveChangesAsync` for any customer with order history (verified against the actual `OrderConfiguration.cs`) — the entire handler would abort AFTER identity anonymization already committed, leaving an unrecoverable half-processed state, and directly contradicting AC #3's "order records are retained" [`ProcessAccountDeletionCommandHandler.cs`] — fixed: addresses are now anonymized in place (`Street`/`City`/`PostalCode`/`Country` scrubbed) instead of hard-deleted, same technique as the name/email anonymization. Added a regression test (`Handle_ShouldNotBreakOrdersReferencingTheAnonymizedAddress`) seeding an `Order` that references the address under deletion.
- [x] [Review][Patch] `IPaymentService.DeleteCustomerDataAsync` only deleted the first matching Stripe Customer (`Limit = 1`) — a customer with multiple Stripe Customer records sharing an email would have the rest left behind, undermining AC #6 [`StripePaymentService.cs`] — fixed: loops over all matches (`Limit = 100`).
- [x] [Review][Patch] `GetAccountDeletionRequestsQuery` returned only `Id`/`UserId`/`Created` — combined with this story's deliberate no-admin-UI scoping (ops tooling only), an admin had no way to identify whose data they were about to irreversibly anonymize without a separate lookup per `UserId` [`AccountDeletionRequestDto.cs`] — fixed: added `Email`, resolved per row via `IIdentityService.GetEmailAsync`.
- [x] [Review][Patch] Frontend: the "Annuler" button stayed clickable while a deletion request was in flight, and clicking it only hid the panel without aborting the in-flight request — if that request then resolved successfully, the UI jumped to "votre demande a bien été reçue" despite the explicit cancel [`profile.component.html`] — fixed: "Annuler" is now `[disabled]` during `accountStore.isLoading()`, same as "Confirmer".
- [x] [Review][Patch] Frontend: deletion failures (e.g. 409 "already pending") were never shown to the user — `confirmAccountDeletion()` unconditionally collapsed the panel regardless of outcome [`profile.component.ts`] — fixed: the panel now stays open and shows the error on failure, using a component-local `deletionError` signal (not bound directly to the shared `accountStore.error()`, to avoid a stale profile-update-form error bleeding into this unrelated panel).

**Deferred (real, architecturally significant, not a quick isolated patch — consistent with pre-existing gaps of the same class elsewhere in this codebase):**

- [x] [Review][Defer] No transaction spans `IIdentityService.AnonymizeUserAsync` (persists immediately via `UserManager`, independent of `_context`) and the rest of `ProcessAccountDeletionCommandHandler`'s `SaveChangesAsync` — a late failure (e.g. a concurrency conflict) could leave the identity already anonymized (AC #5 satisfied) while the audit trail (AC #4: `Status`/`ProcessedByAdminUserId`/`ProcessedAt`) never gets stamped and refresh tokens stay unrevoked. Deferred: fixing this properly requires `UserManager` and `IApplicationDbContext` to share a transaction/connection scope, which no code in this codebase sets up anywhere today — `AccountService.UpdateProfileAsync` (Story 2.4) has the exact same unaddressed gap for its own `UserManager` + implicit persistence calls. Revisit if this pattern needs to be solved generally, not as a one-off for this story.
- [x] [Review][Defer] TOCTOU race in `RequestAccountDeletionCommandHandler`'s idempotency guard (`AnyAsync` check with no supporting unique/filtered index) — two concurrent submits could both pass the check before either commits, creating two `Pending` rows for one user. Deferred: same class of gap as every other check-then-insert guard in this codebase (no unique-index precedent anywhere), low real-world likelihood (would need a genuine double-click/two-tab race).
- [x] [Review][Defer] No concurrency token on `AccountDeletionRequest` — two admins (or a retried request) processing the same id concurrently could both pass the `Status == Pending` check and corrupt `ProcessedByAdminUserId`/`ProcessedAt`. Deferred: `Return` (the closest analogous entity, Story 5.x-7.x) has the identical gap, unaddressed — not something to solve as a one-off here.
- [x] [Review][Defer] `ConflictException` (409) is used both for "request already processed" and for `AnonymizeUserAsync` returning `Result.Failure` when the target user doesn't exist — two semantically different failures collapsed into one status code. Deferred: the "user not found" case can only occur if referential integrity is already broken (extremely rare); properly distinguishing it would need `ProcessAccountDeletionCommand` to return a `Result` instead of being a bare `IRequest`, a larger change than this finding's severity justifies.

**Dismissed as noise / already correct:**

- "Migration's `nvarchar(450)` for `UserId` vs `nvarchar(max)` for `ProcessedByAdminUserId` looks inconsistent/possibly not tool-generated" — false positive, verified: every other `UserId` column in this codebase's entire migration history (`Return`, `PaymentAuditLog`, `RefreshToken`, etc.) is `nvarchar(450)` under the identical no-explicit-`MaxLength` configuration pattern — this is standard, not suspicious.
- "`AnonymizeUserAsync` is missing the `CancellationToken` param the story's own Subtask 5.1 specified" — intentional, correct deviation: `UserManager.SetEmailAsync`/`SetUserNameAsync`/`FindByIdAsync` accept no `CancellationToken`, and `IIdentityService`'s own established convention has zero `CancellationToken` params on any method — adding a decorative unused one would be worse, not better. Documented with an inline comment to preempt future confusion.
- "`customerEmail ?? string.Empty` silently coerces a null email instead of failing loudly" (both handlers) — matches `CreateReturnRequestCommandHandler`'s identical existing pattern; not a new regression introduced by this story.

## Dev Notes

### This codebase's admin surface is backend-API-only — no Angular admin UI exists anywhere

Confirmed via `frontend/mon-ecommerce-web/src/app/app.routes.ts` (zero `/admin` routes) and a full directory search (no `features/admin/` anywhere) — despite Epic 6 (Administration Catalogue) and Epic 7 (Administration Commandes & Dashboard) both being marked `done` in `sprint-status.yaml`. Every "admin" story in this codebase shipped backend endpoints only. This story follows the same established scope: `GetAccountDeletionRequestsQuery` + `ProcessAccountDeletionCommand` are real, callable, role-gated endpoints, but no Angular page is built to drive them — building one here would be new, unrequested scope disproportionate to a single story, and inconsistent with how every prior admin story in this codebase was actually delivered.

### No Stripe Customer object exists anywhere in this codebase today

`StripePaymentService.CreatePaymentIntentAsync` (used by the entire checkout flow, Story 4.5) creates `PaymentIntent`s directly with no `Customer` parameter — this system has never created a Stripe Customer for any real order. AC #6 ("Stripe customer data deletion is also requested") is satisfied by implementing the *request* correctly (search-by-email, delete-if-found) — it is expected to be a no-op for essentially every real customer today. Do not add Customer creation to the checkout flow to "give this AC something to delete" — that would be new, unrequested scope for the checkout story, not this one.

### The `AccountDeletionRequest` row IS the audit log (AC #4) — no separate audit table

Same convention as `Return`/`PaymentAuditLog`: `Status`, `ProcessedByAdminUserId`, `ProcessedAt` on the entity itself satisfy "logged with timestamp and processing admin." Do not build a separate `AccountDeletionAuditLog` entity — it would duplicate data already captured on the one row this story creates.

### Anonymizing the email IS what blocks old-credential login (AC #5) — no separate lockout mechanism needed

ASP.NET Identity resolves users by email/username at login. Once `ProcessAccountDeletionCommandHandler` changes both to the irreversible hash, the customer's old email + password can never resolve to this account again — this is sufficient and is the correct mechanism, not a side effect to work around. Refresh-token revocation (Subtask 5.3) is an *additional* hardening step for already-issued sessions, since AC #5's intent (a right-to-erasure request) implies more than just blocking new login attempts.

### `Order` needs no changes for AC #3 — it already carries no personal identifiers of its own

`Order` (checked: `Domain/Entities/Order.cs`) has no `CustomerName`/`CustomerEmail` fields — only `UserId` (FK) and `ShippingAddressId` (FK). Anonymizing the referenced `ApplicationUser` and hard-deleting the referenced `Address` already removes every personal identifier reachable from an `Order`, satisfying AC #3 with zero `Order`-table changes. Do not add anonymization logic to `Order` itself — there is nothing there to anonymize.

### Backend test project uses NUnit + Moq + EF InMemory, with a REAL `UserManager` for anything touching Identity

Confirmed in `tests/Application.UnitTests/`: `*HandlerTests.cs` use `DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(...)` + `Moq` mocks for interfaces like `IIdentityService`/`IPaymentService` (see `UpdateReturnStatusCommandHandlerTests.cs`). But `AccountServiceTests.cs` builds a REAL `UserManager<ApplicationUser>` via a real `AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>()` DI container (with weakened password rules for test speed) rather than mocking `UserManager` — its own comment explains why: "the thing under test includes Identity's own password/email validation, which a mock would trivially fake." `IdentityService.AnonymizeUserAsync`'s tests must follow the same real-`UserManager` pattern (it's exactly the same class of Identity-touching code as `AccountService`). `ProcessAccountDeletionCommandHandlerTests`, by contrast, should mock `IIdentityService` itself (same as `UpdateReturnStatusCommandHandlerTests` mocks it) — the handler doesn't touch `UserManager` directly, so there's nothing Identity-specific to fake there.

### Immediate request (customer-facing) vs. processing (admin-facing) are two distinct, separately-timed actions

AC #1's "confirmation email... within 30 seconds" fires at *request* time (customer submits the form) using their still-real email. AC #2's anonymization fires later, at *processing* time (admin action, "within 30 days"), and is when the email actually becomes an irreversible hash. Do not conflate the two — the request-confirmation email must never be deferred until processing (the email address wouldn't be usable anymore by then), and processing must never re-send a request-confirmation email (that was already sent at request time).

### Project Structure Notes

New (backend):
- `backend/MonEcommerce/src/Domain/Entities/AccountDeletionRequest.cs`
- `backend/MonEcommerce/src/Domain/Enums/AccountDeletionStatus.cs`
- `backend/MonEcommerce/src/Domain/Events/AccountDeletionRequestedEvent.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/AccountDeletionRequestConfiguration.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/*_AddAccountDeletionRequest.cs`
- `backend/MonEcommerce/src/Application/Account/Commands/RequestAccountDeletionCommand.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/Commands/ProcessAccountDeletionCommand.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/Queries/GetAccountDeletionRequestsQuery.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/EventHandlers/AccountDeletionRequestedEmailHandler.cs`
- `backend/MonEcommerce/src/Web/Endpoints/AdminAccountDeletions.cs`
- Backend test files mirroring the above (handler unit tests, same xUnit conventions as `CreateReturnRequestCommandHandlerTests`/`UpdateReturnStatusCommandHandlerTests` if those exist — check `backend/MonEcommerce/tests/` for the exact test project structure and naming before writing new ones).

Modified (backend):
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (+ `DbSet<AccountDeletionRequest>`)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (+ `DbSet<AccountDeletionRequest>`)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IPaymentService.cs` (+ `DeleteCustomerDataAsync`)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/StripePaymentService.cs` (implements it)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (+ `CustomerService` registration)
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs` (+ `delete-request` route)

New (frontend):
- None — `profile.component.{html,ts,spec.ts}` and `account.store.{ts,spec.ts}` are modified in place, no new files.

Modified (frontend):
- `frontend/mon-ecommerce-web/src/app/features/account/account.store.ts` (+ `requestAccountDeletion`, `deletionRequested` state) + its spec
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.{html,ts}` (+ "Supprimer mon compte" section) + its spec

No mobile changes — Epic 8 is web-only scope per every prior story in this epic (8.1, 8.2 were both Angular-only).

### References

- `_bmad-output/planning-artifacts/epics.md` — Story 8.3 acceptance criteria (Epic 8 section, line ~1321).
- `_bmad-output/planning-artifacts/prd.md:156` — "RGPD : ... droit à l'oubli et portabilité des données."
- `backend/MonEcommerce/src/Application/Returns/Commands/CreateReturnRequestCommand{,Handler}.cs` — customer-facing request command pattern (`[Authorize]`, `IUser`, domain event on create).
- `backend/MonEcommerce/src/Application/Returns/Commands/UpdateReturnStatusCommandHandler.cs` — admin-facing "process a pending request" pattern (already-processed guard, no user-scoping).
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` — email-on-domain-event pattern (try/catch, log-and-swallow, `EmailTemplateBuilder.Wrap`).
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` — email-change-via-`UserManager` pattern (`SetEmailAsync` + `SetUserNameAsync` kept in sync).
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/StripePaymentService.cs` — existing Stripe.net service wrapper pattern to extend with `CustomerService`.
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs:148-155` — Stripe service DI registration block.
- `backend/MonEcommerce/src/Domain/Entities/RefreshToken.cs` — `RevokedAt`/`IsRevoked` fields to use for session revocation.
- `frontend/mon-ecommerce-web/src/app/features/account/account.store.ts` — `updateProfile`'s state/error-handling pattern to mirror for `requestAccountDeletion`.
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.ts` — page this story extends.

## Dev Agent Record

### Agent Model Used

Claude Opus 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 errors after fixing one hand-written `IIdentityService` stub (`AuthorizationPipelineTests.StubIdentityService`) that needed the new `AnonymizeUserAsync` member. `dotnet test MonEcommerce.sln --filter "FullyQualifiedName!~IntegrationTests"` — 395/395 passing (Domain.UnitTests/Infrastructure.IntegrationTests report "no test matches" — Domain.UnitTests is a pre-existing empty scaffold with 0 tests, Infrastructure.IntegrationTests needs the unreachable `DESKTOP-M36577B` SQL Server and was correctly excluded).
- **Environment gap, not a code issue**: this machine only has .NET 10 SDKs installed (`10.0.301`, `10.0.302`), but `global.json` pins `9.0.101` with `rollForward: latestFeature` (doesn't cross major versions) — `dotnet build`/`dotnet ef` fail outright with "compatible .NET SDK was not found" using the committed `global.json`. Worked around **locally, non-persistently** by temporarily flipping `rollForward` to `latestMajor`, building/testing/generating the migration, then reverting `global.json` to its original committed value before finishing — this file is unchanged in the final diff. Flagging this for the user: either install a .NET 9 SDK on this machine, or deliberately decide to bump `global.json` to track .NET 10 (a real decision with its own implications, not something to change as a side effect of a GDPR story).
- EF migration `AddAccountDeletionRequest` generated successfully (`dotnet ef migrations add`) and inspected — schema matches the entity exactly (table + two indexes). `dotnet ef database update` could not run: `DESKTOP-M36577B` SQL Server is unreachable from this environment — same pre-existing gap Story 2.1's Dev Agent Record already flagged, not new to this story.
- Frontend: `ng test --watch=false --browsers=ChromeHeadless` — 182/182 passing (154 baseline for Epic 8 stories + 28 net new: 6 backend-driven store/component tests + a handful from restructured specs). `ng build` succeeds, prerendering unaffected (16 static routes, same as before — this story adds no new routes).

### Completion Notes List

- Backend (.NET): new `AccountDeletionRequest` entity/enum/domain event, `RequestAccountDeletionCommand` (customer, AC #1, idempotent — 409 if already pending), `AccountDeletionRequestedEmailHandler` (AC #1's confirmation email, sent with the customer's still-real email at request time), `IIdentityService.AnonymizeUserAsync` (new Application-layer capability so the Clean Architecture boundary isn't crossed), `ProcessAccountDeletionCommand` (admin-only, AC #2/#3/#4/#5/#6 — anonymizes name/email via the new `IIdentityService` method, revokes active refresh tokens, hard-deletes addresses, best-effort requests Stripe customer deletion, stamps `ProcessedByAdminUserId`/`ProcessedAt` as the audit trail), `GetAccountDeletionRequestsQuery` (admin-only, FIFO list). Two new endpoints: `POST /api/v1/account/delete-request` and the new `AdminAccountDeletions` group (`GET`/`POST .../process`) — no Angular admin UI, consistent with this codebase's backend-API-only admin surface across Epics 6–7 (verified: zero `/admin` routes exist anywhere in the frontend).
- Confirmed via code inspection: this codebase's checkout flow never creates a Stripe `Customer` object, so `IPaymentService.DeleteCustomerDataAsync` (AC #6) is a defensive search-and-delete-if-found — expected to be a no-op for essentially every real customer today, exactly as flagged in the story's Dev Notes.
- Frontend (Angular): `AccountStore.requestAccountDeletion()` + `deletionRequested` state; `ProfileComponent` gained a "Supprimer mon compte" section with an inline confirm/cancel panel (no native `confirm()` dialog) that does NOT log the user out or disable the rest of the page — the account stays usable until an admin processes the request, per AC #2's "within 30 days."
- Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) found 1 CRITICAL bug confirmed by all three layers independently — hard-deleting `Address` rows would throw an FK-Restrict violation (`Order.ShippingAddressId`) for any customer with order history, aborting the handler after identity anonymization already committed. Fixed by anonymizing address content in place instead of deleting rows, with a regression test proving an `Order` referencing the address survives. 4 more patches applied (Stripe multi-customer deletion, admin query email field, frontend Annuler-during-loading + error surfacing). 4 items deferred (transaction boundary, 2 concurrency races, exception-type overloading) — all consistent with pre-existing unaddressed gaps of the same class elsewhere in this codebase, not new regressions. 3 items dismissed as false positives / already-correct-by-design. Final: `dotnet test` 397/397 passing, `ng test` 183/183 passing, `ng build` clean.

### File List

**New (backend):**
- `backend/MonEcommerce/src/Domain/Entities/AccountDeletionRequest.cs`
- `backend/MonEcommerce/src/Domain/Enums/AccountDeletionStatus.cs`
- `backend/MonEcommerce/src/Domain/Events/AccountDeletionRequestedEvent.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/AccountDeletionRequestConfiguration.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260805235654_AddAccountDeletionRequest.cs` (+ `.Designer.cs`)
- `backend/MonEcommerce/src/Application/Account/Commands/RequestAccountDeletionCommand.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/Commands/ProcessAccountDeletionCommand.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/Queries/GetAccountDeletionRequestsQuery.cs` (+ Handler)
- `backend/MonEcommerce/src/Application/Account/Models/AccountDeletionRequestDto.cs`
- `backend/MonEcommerce/src/Application/Account/EventHandlers/AccountDeletionRequestedEmailHandler.cs`
- `backend/MonEcommerce/src/Web/Endpoints/AdminAccountDeletions.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Commands/RequestAccountDeletionCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Commands/ProcessAccountDeletionCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/EventHandlers/AccountDeletionRequestedEmailHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/IdentityServiceAnonymizeUserAsyncTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Queries/GetAccountDeletionRequestsQueryHandlerTests.cs`

**Modified (backend):**
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (+ `AccountDeletionRequests` DbSet)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (+ DbSet)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IIdentityService.cs` (+ `AnonymizeUserAsync`)
- `backend/MonEcommerce/src/Infrastructure/Identity/IdentityService.cs` (implements it)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IPaymentService.cs` (+ `DeleteCustomerDataAsync`)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/StripePaymentService.cs` (implements it)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (+ `CustomerService` registration)
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs` (+ `delete-request` route)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/AuthorizationPipelineTests.cs` (stub updated for the new `IIdentityService` member)

**Modified (frontend):**
- `frontend/mon-ecommerce-web/src/app/features/account/account.store.ts` (+ `requestAccountDeletion`, `deletionRequested`) + `.spec.ts`
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.{ts,html}` (+ "Supprimer mon compte") + `.spec.ts`

**Not modified (deliberately):** `backend/MonEcommerce/global.json` — temporarily edited locally to work around this environment's missing .NET 9 SDK, then reverted before finishing; no diff.
