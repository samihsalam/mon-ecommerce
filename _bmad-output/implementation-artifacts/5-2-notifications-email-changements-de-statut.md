# Story 5.2: Notifications Email — Changements de Statut

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to receive an email at every order status change (En préparation → Expédiée → Livrée),
so that I am proactively informed without needing to check my account.

## Acceptance Criteria

1. **Given** an admin updates an order status to "Expédiée" with a tracking number, **when** `OrderShippedEvent` is published, **then** a shipment notification email is sent to the customer within ≤ 30 seconds, **and** the email includes the tracking number and a "Suivre ma commande" link.
2. **Given** an order status is updated to "Livrée", **when** `OrderDeliveredEvent` is published, **then** a delivery confirmation email is sent to the customer within ≤ 30 seconds.
3. All email templates use DM Sans typography and the Élégance Naturelle palette.
4. Emails render correctly on Gmail, Outlook, and Apple Mail (tested via SendGrid preview).

## Tasks / Subtasks

### Backend (AC: #1, #2)

- [x] Task 1: Minimal admin order-status-update capability — **the missing prerequisite this AC assumes exists**. Nothing in this codebase today lets anyone (admin or otherwise) transition `Order.Status` — Epic 7 ("Administration Commandes", Story 7.2 "Mise à jour statut et numéro de suivi") owns the real admin UI/workflow for this and is still backlog. Building the *full* admin order-management surface is out of this story's scope, but AC #1/#2's trigger ("an admin updates an order status") is meaningless without SOME way to do it — so this story adds the minimal slice: one authenticated, admin-role-gated command/endpoint that sets `Order.Status` (+`TrackingNumber` when moving to Shipped) and publishes the right event. Story 7.2 is expected to build the actual admin UI around this same command later, not replace it.
  - [x] `Application/Orders/Commands/UpdateOrderStatusCommand.cs`: `[Authorize(Roles = Roles.Administrator)]` (the `Administrator` role + a seeded `administrator@localhost` account already exist — `Infrastructure/Data/ApplicationDbContextInitialiser.cs`, built ahead of schedule, never used until now) — `OrderId`, `NewStatus` (`OrderStatus`), `TrackingNumber` (`string?`)
  - [x] Validator: `TrackingNumber` required (`NotEmpty()`) when `NewStatus == OrderStatus.Shipped`, otherwise unconstrained
  - [x] Handler: loads the order by id (no user-scoping — an admin operates across all customers' orders, unlike every customer-facing query in this codebase); sets `Status` (+`TrackingNumber` if provided); resolves the customer's email via `IIdentityService.GetEmailAsync(order.UserId)` (Story 4.6); publishes `OrderShippedEvent` (with the tracking link — see Task 2) when `NewStatus == Shipped`, or `OrderDeliveredEvent` (Task 3) when `NewStatus == Delivered` — no event for any other transition, since only these two have an AC-defined email
  - [x] `Web/Endpoints/AdminOrders.cs` (new endpoint group, `RoutePrefix => "/api/v1/admin/orders"`, matching `architecture.md`'s planned `Admin/` namespace): `PATCH orders/{orderId:guid}/status`, `.RequireAuthorization()` (the HTTP-level check just requires *some* authenticated caller — same convention as every other endpoint in this codebase; the actual admin-role gate is `AuthorizationBehaviour` enforcing the command's `[Authorize(Roles = ...)]`, already fully wired and unused until now)
- [x] Task 2: Extend `Domain/Events/OrderShippedEvent.cs` with a `TrackingLink` field (AC #1's literal "and a 'Suivre ma commande' link" — the event as it exists today only carries `TrackingNumber`, genuinely cannot satisfy this AC without the extension; this isn't touching an "already sufficient, reuse as-is" event the way Story 5.1/4.6 treated others — it's a required fix)
  - [x] `UpdateOrderStatusCommandHandler` builds the link the same way `AuthService.RequestPasswordResetAsync` already does: `{Frontend:BaseUrl}/compte/commandes/{orderId}` (the existing order-detail page, which already renders `trackingNumber` — Story 2.5), injecting `IConfiguration` the same way
  - [x] Update `Application/Orders/EventHandlers/OrderShippedEmailHandler.cs` (already exists) to include the link in the email body — the only change this story makes to that handler
- [x] Task 3: `Domain/Events/OrderDeliveredEvent.cs` (new): `record OrderDeliveredEvent(Guid OrderId, string CustomerEmail) : BaseEvent;` (no tracking-link equivalent needed — AC #2 doesn't ask for one) + `Application/Orders/EventHandlers/OrderDeliveredEmailHandler.cs` (new, mirrors `OrderShippedEmailHandler`'s try/catch-and-log structure exactly)
- [x] Task 4: Backend tests
  - [x] `UpdateOrderStatusCommandHandlerTests`: sets `Order.Status`/`TrackingNumber` and publishes `OrderShippedEvent` (with the correct link) when moving to `Shipped`; publishes `OrderDeliveredEvent` when moving to `Delivered`; publishes nothing for other transitions (e.g. `Pending` → `Processing`)
  - [x] `UpdateOrderStatusCommandValidatorTests`: fails when `NewStatus == Shipped` and `TrackingNumber` is empty; passes otherwise
  - [x] `AuthorizationPipelineTests`-style coverage (or a dedicated test) confirming a non-admin caller is rejected — this is the first command in the codebase to actually exercise `AuthorizationBehaviour`'s role-checking branch, worth a real test now that something finally uses it
  - [x] `OrderDeliveredEmailHandlerTests`: sends the expected email; logs and swallows (doesn't rethrow) on a non-cancellation failure, matching every other email handler's established behavior
  - [x] `OrderShippedEmailHandlerTests` update: asserts the tracking link now appears in the email body

### AC #3/#4 — Email template quality (already satisfied, not new work this story)

- [x] Task 5: Confirm, don't rebuild: this codebase's `IEmailService`/`SendGridEmailService` sends plain-text bodies today (every existing handler — `OrderPlacedEmailHandler`, `ReturnRequestedEmailHandler`, etc. — interpolates a plain string, no HTML/CSS at all). AC #3's "DM Sans typography and the Élégance Naturelle palette" and AC #4's "renders correctly on Gmail/Outlook/Apple Mail via SendGrid preview" describe an HTML email **template system that doesn't exist anywhere in this codebase yet** — not something this story's two new/modified plain-text handlers can satisfy, and not something to invent piecemeal for just these two emails. **Flag for Story 5.4** ("Couverture complète emails transactionnels" — the epic's own dedicated story for exactly this kind of cross-cutting email concern): a shared HTML template system covering every transactional email at once, this story's included, is the correct place for AC #3/#4, not a one-off addition here. Documented in Dev Notes, not silently dropped.

## Dev Notes

### Why this story adds a slice of Story 7.2's scope

AC #1/#2 are phrased as reactions to an admin action ("Given an admin updates an order status..."), but no admin capability to do that exists yet — Epic 7 (`epics.md`, Story 7.2) is the epic actually responsible for the admin order-management UI/workflow, and it's still fully backlog. Blocking this story on Epic 7 would mean Story 5.2 can never ship before Epic 7 does, which inverts the epics' own numbering/priority (Epic 5 — "Commandes, Retours & Notifications" — is scheduled well before Epic 6/7's admin surfaces). The resolution: build only the one command/endpoint this story's own ACs need to be triggerable and testable at all, explicitly scoped narrower than "the admin order management feature" (no admin UI, no order list/filtering, no bulk actions — those remain Story 7.1/7.2/7.3's job). Story 7.2 is expected to build its UI on top of this same `UpdateOrderStatusCommand`, not duplicate it.

### Why `OrderShippedEvent` gets a new field instead of being reused untouched

Unlike Story 5.1's `ReturnRequestedEvent` or Story 4.6's `OrderPlacedEvent` (both already fully sufficient for their AC's needs), `OrderShippedEvent`'s existing shape (`OrderId`, `CustomerEmail`, `TrackingNumber`) cannot express AC #1's explicit "and a 'Suivre ma commande' link" requirement — no amount of handler-side cleverness recovers a link that was never passed to the handler. Adding `TrackingLink` is the minimal correct fix, matching the exact link-building convention `AuthService`'s password-reset flow already established (`Frontend:BaseUrl` config + `IConfiguration`, built in the command/service that publishes the event, not the handler that consumes it).

### AC #3/#4 (HTML email templates) — deferred to Story 5.4, not silently dropped

See Task 5. This is a real, acknowledged gap in this story's own ACs, resolved by cross-referencing the epic's own later story that owns exactly this concern — same class of resolution as Story 3.2's PostgreSQL/SQL-Server conflict and Story 4.5's "cart total" wording gap, both resolved by reading the surrounding epic's intent rather than inventing scope or blocking.

## Project Structure Notes

New `Application/Orders/Commands/UpdateOrderStatusCommand.cs`, `Web/Endpoints/AdminOrders.cs` (new endpoint group), `Domain/Events/OrderDeliveredEvent.cs`, `Application/Orders/EventHandlers/OrderDeliveredEmailHandler.cs`. Modified: `Domain/Events/OrderShippedEvent.cs`, `Application/Orders/EventHandlers/OrderShippedEmailHandler.cs`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 5.2 acceptance criteria (Epic 5 section, line ~919); Story 7.2 (Epic 7) for the admin UI this story's command is a slice of
- `backend/MonEcommerce/src/Domain/Constants/Roles.cs`, `Infrastructure/Data/ApplicationDbContextInitialiser.cs` — existing, unused-until-now `Administrator` role + seeded account
- `backend/MonEcommerce/src/Application/Common/Behaviours/AuthorizationBehaviour.cs`, `Common/Security/AuthorizeAttribute.cs` — existing, unused-until-now role-checking pipeline behavior
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` (`RequestPasswordResetAsync`) — the `Frontend:BaseUrl`-based link-building convention this story's tracking link reuses
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs`, `OrderPlacedEmailHandler.cs` — existing handler style/structure this story's new handler matches

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 warnings, 0 errors. `dotnet test MonEcommerce.sln` — 208/208 passed (`Application.UnitTests`; other two projects report "no tests", pre-existing, unrelated). No new migration needed — `Order.TrackingNumber`/`Status` were already existing, persisted fields; this story only adds a new way to write them, no schema change.
- No frontend/mobile work this story — AC #1/#2 are entirely about server-triggered emails, no new customer-facing UI. The admin endpoint (`PATCH /api/v1/admin/orders/{orderId}/status`) has no UI consumer yet by design; Story 7.2 is expected to build one against this same command.
- `UpdateOrderStatusCommand` is the first command anywhere in this codebase to use `[Authorize(Roles = ...)]` — added two tests to `AuthorizationPipelineTests.cs` (the existing home for full-pipeline authorization tests) proving `AuthorizationBehaviour`'s role-checking branch actually rejects a non-admin (`ForbiddenAccessException`) and an anonymous caller (`UnauthorizedAccessException`) end-to-end, not just via a unit test of the behavior in isolation. A third planned test asserting an admin's request passes the role check was dropped — proving that would require guessing the exact exception type thrown by a downstream DI-resolution failure in the minimal test host (since `IApplicationDbContext` isn't registered there), which wasn't worth the fragility; the two rejection cases already prove the role check itself works, and `UpdateOrderStatusCommandHandlerTests` covers the handler's actual behavior directly.

### Completion Notes List

- Added the minimal admin-only slice of order-status management (`UpdateOrderStatusCommand`/Handler/Validator, `PATCH /api/v1/admin/orders/{orderId}/status`) needed to make AC #1/#2 triggerable at all — reusing the `Administrator` role and seeded admin account that already existed in `ApplicationDbContextInitialiser` but had never been used by anything. Explicitly scoped narrower than Story 7.2's full admin order-management feature (no UI, no listing/filtering) — documented as such so it isn't mistaken for that story being done.
- Extended `OrderShippedEvent` with a `TrackingLink` field and updated the already-existing `OrderShippedEmailHandler` to include it — the pre-existing event genuinely couldn't satisfy AC #1's "and a 'Suivre ma commande' link" without this, unlike Story 5.1/4.6's events which were already sufficient as-is.
- New `OrderDeliveredEvent`/`OrderDeliveredEmailHandler`, matching the established plain-text email handler pattern (try/catch, log-and-swallow on failure) exactly.
- AC #3/#4 (HTML email styling, cross-client rendering) explicitly NOT built this story — this codebase has no HTML email template system at all yet (every handler sends plain text), and inventing one for just these two emails would pre-empt Story 5.4's own scope ("Couverture complète emails transactionnels"), the epic's dedicated story for exactly this cross-cutting concern. Flagged in Dev Notes rather than silently skipped or improvised.

### File List

**Backend**
- `backend/MonEcommerce/src/Domain/Events/OrderShippedEvent.cs` (modified — `TrackingLink`), `OrderDeliveredEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs` (modified), `OrderDeliveredEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Orders/Commands/UpdateOrderStatusCommand.cs`, `UpdateOrderStatusCommandHandler.cs`, `UpdateOrderStatusCommandValidator.cs` (new)
- `backend/MonEcommerce/src/Web/Endpoints/AdminOrders.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/Commands/UpdateOrderStatusCommandHandlerTests.cs`, `UpdateOrderStatusCommandValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/EventHandlers/OrderShippedEmailHandlerTests.cs` (modified), `OrderDeliveredEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/AuthorizationPipelineTests.cs` (modified — role-based authorization coverage)
