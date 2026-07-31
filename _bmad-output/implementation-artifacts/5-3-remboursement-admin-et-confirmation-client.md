# Story 5.3: Remboursement Admin & Confirmation Client

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to validate a return request and issue a Stripe refund,
so that I can close the return cycle and maintain customer trust.

## Acceptance Criteria

1. **Given** a pending return request, **when** `PATCH /api/v1/admin/returns/{returnId}` is called with status "Validé", **then** the return status is updated and the customer is notified.
2. **Given** the return is validated, **when** `POST /api/v1/admin/returns/{returnId}/refund` is called, **then** the Stripe Refund API is called for the original payment amount, **and** `RefundIssuedEvent` is published → customer receives a refund confirmation email within ≤ 30 seconds.
3. **Given** a Stripe refund API failure, **when** the refund call fails, **then** the error is logged, an alert is raised for the admin, and no partial state is persisted.
4. The refund email includes: amount refunded, original order number, and expected processing time (3–5 business days).
5. A complete audit trail is stored: amount, date, admin user, Stripe refund ID.
6. Only admins with the `Administrator` role can issue refunds.

## Tasks / Subtasks

### Backend (AC: #1, #2, #3, #5, #6)

- [x] Task 1: `Application/Returns/Commands/UpdateReturnStatusCommand.cs` + Handler + Validator (AC #1)
  - [x] `[Authorize(Roles = Roles.Administrator)]` — `ReturnId`, `NewStatus` (`ReturnStatus`) — supports both `Approved` ("Validé") and `Rejected`, not just the AC's one literal example; a reject path is the obvious mirror case and costs nothing extra to include now
  - [x] Validator: `NewStatus` must be `Approved` or `Rejected` (not `Pending`/`Refunded` — those aren't valid admin-initiated transitions from this endpoint; `Refunded` is set by Task 2's refund command, not this one)
  - [x] Handler: loads the `Return` by id (no user-scoping — admin-wide, same as `UpdateOrderStatusCommandHandler`), rejects with `ConflictException` if it isn't currently `Pending` (an already-decided return can't be re-decided through this endpoint), sets `Status`, resolves the customer's email via `IIdentityService.GetEmailAsync`, publishes `ReturnStatusUpdatedEvent` (Task 3)
  - [x] **Fix while here**: `AccountService.MapReturnStatusLabel`'s `Approved` label was "Approuvé" (Story 5.1) — this AC's literal wording is "Validé"; update the label to match exactly, since this story's own PATCH request body will use that exact string
- [x] Task 2: `Application/Returns/Commands/IssueReturnRefundCommand.cs` + Handler + Validator (AC #2, #3, #5, #6)
  - [x] `[Authorize(Roles = Roles.Administrator)]` — `ReturnId` only (no amount parameter — AC #2 explicitly says "the original payment amount", not an admin-chosen one, matching the same "server is the source of truth for money" principle established since Story 4.5/4.6)
  - [x] Handler: loads the `Return` + its `Order` (`Order.StripePaymentIntentId` — added in Story 4.6 — is what actually gets refunded); throws `ConflictException` if `Return.Status != ReturnStatus.Approved` (AC #2's "given the return is validated" precondition) or if `Order.StripePaymentIntentId` is somehow null (can't happen for a real paid order, but a real historical/seeded edge case is worth a clear error over a null-ref)
  - [x] **AC #3, "no partial state is persisted"**: call `IPaymentService.CreateRefundAsync(order.StripePaymentIntentId, order.TotalInCents, ct)` (already exists, Story 4.6) *before* touching `Return.Status` or writing any `PaymentAuditLog` row in the tracked context — if it throws, propagate a new `RefundFailedException` (→ 502, Task 4) without ever calling `SaveChangesAsync`, so nothing partial is written. Only after the Stripe call succeeds: set `Return.Status = ReturnStatus.Refunded`, insert a `PaymentAuditLog` row (`Outcome = Refunded`, `AdminUserId` = the calling admin's id via `IUser`, `StripeRefundId` = the id `CreateRefundAsync` returned — Task 5), publish `RefundIssuedEvent` (Task 6), then `SaveChangesAsync` once
  - [x] **AC #3, "an alert is raised for the admin"**: this codebase has no paging/alerting integration — `_logger.LogError` (the same "alert" mechanism every other handler in this codebase already uses for failures worth someone's attention) is the correct, consistent interpretation, not a new integration to build
- [x] Task 3: `Domain/Events/ReturnStatusUpdatedEvent.cs` (new): `record ReturnStatusUpdatedEvent(Guid ReturnId, Guid OrderId, string CustomerEmail, string NewStatus) : BaseEvent;` + `Application/Returns/EventHandlers/ReturnStatusUpdatedEmailHandler.cs` (new) — one email, wording varies by `NewStatus` ("Validé" vs "Refusé"), matching the established try/catch/log-and-swallow handler shape exactly
- [x] Task 4: `Application/Common/Exceptions/RefundFailedException.cs` (new) → `ProblemDetailsExceptionHandler` mapping to `502 Bad Gateway` (a genuine upstream/Stripe failure, distinct from every existing 4xx mapping — none of which mean "an external payment provider call failed")
- [x] Task 5: Extend `Domain/Entities/PaymentAuditLog.cs` with `AdminUserId` (`string?`, null for the Story 4.6 webhook-originated rows, set for this story's admin-issued refunds) and `StripeRefundId` (`string?`, same nullability split) — new migration. `PaymentAuditLogConfiguration` needs no new index; the existing `StripePaymentIntentId` index still finds all rows (webhook-originated and admin-refund-originated alike) for a given payment
- [x] Task 6: Extend `Domain/Events/RefundIssuedEvent.cs` with `OrderNumber` (`string`, pre-formatted — see Task 7) — AC #4 requires "original order number" in the email, and the event as it exists today only carries a raw `OrderId` Guid, which isn't what a customer-facing email should print (every other customer-facing order reference in this codebase uses the `#XXXXXXXX` format, e.g. `AccountService.FormatOrderNumber`)
  - [x] Update `Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs` (already exists — built ahead of schedule, correctly UNUSED until now; Story 4.6 deliberately did NOT reuse it for the anti-overselling refund case since no `Order` existed there, and explicitly flagged this story as its real, intended use case) — update the email body to include `OrderNumber` and the literal "3 à 5 jours ouvrés" processing-time text (AC #4)
- [x] Task 7: Extract `FormatOrderNumber` out of `AccountService` (currently `private static`) into a small shared `Application/Common/OrderNumberFormatter.cs` (`public static string Format(Guid orderId)`) — this is now needed in two unrelated places (`AccountService` and `IssueReturnRefundCommandHandler`); duplicating the exact one-liner instead would be the second copy of logic this codebase has specifically flagged as worth sharing (see `ShippingOptionsCatalog`'s Story 4.5 Dev Notes on the same "single source of truth" reasoning). `AccountService` updated to call the shared formatter instead of its own private copy — no behavior change there.
- [x] Task 8: `Web/Endpoints/AdminReturns.cs` (new endpoint group, `RoutePrefix => "/api/v1/admin/returns"`): `PATCH {returnId:guid}` (Task 1's command) and `POST {returnId:guid}/refund` (Task 2's command), both `.RequireAuthorization()` (HTTP-level; the real admin-role gate is each command's own `[Authorize(Roles = ...)]`, same split as Story 5.2's `AdminOrders.cs`)
- [x] Task 9: Backend tests
  - [x] `UpdateReturnStatusCommandHandlerTests`: approves/rejects a `Pending` return and publishes `ReturnStatusUpdatedEvent`; throws `ConflictException` for a non-`Pending` return; throws for an invalid `NewStatus` via the validator
  - [x] `IssueReturnRefundCommandHandlerTests`: calls `IPaymentService.CreateRefundAsync` with the order's `StripePaymentIntentId` and `TotalInCents`; sets `Return.Status = Refunded`, writes a `PaymentAuditLog` with the right `AdminUserId`/`StripeRefundId`, and publishes `RefundIssuedEvent` (with `OrderNumber`) on success; throws `RefundFailedException` **and verifies nothing was persisted** (no `PaymentAuditLog` row, `Return.Status` unchanged) when `CreateRefundAsync` throws; throws `ConflictException` for a non-`Approved` return
  - [x] `RefundIssuedEmailHandlerTests` (new — none existed before, since this handler was never used): sends the expected email including the order number and "3 à 5 jours ouvrés"
  - [x] `ReturnStatusUpdatedEmailHandlerTests` (new)
  - [x] Authorization coverage (same `AuthorizationPipelineTests.cs` pattern as Story 5.2) confirming a non-admin is rejected on both new commands

## Dev Notes

### `RefundIssuedEvent`/`RefundIssuedEmailHandler` — this is their intended use case

Story 4.6 explicitly did NOT reuse this event/handler for its own refund path (stock ran out before an order existed, so there was no real `OrderId`/order number to reference) and built a separate `StockUnavailableEvent` instead, flagging in its own Dev Notes that this event was built ahead of schedule for a return-refund scenario that hadn't been implemented yet. This story is that scenario — extending the event (Task 6) rather than leaving it as a dead, never-exercised piece of code.

### Why "no partial state persisted" means calling Stripe before touching the database, not a transaction

The simplest way to guarantee AC #3 is to make the external call first and only start mutating tracked entities / building the `SaveChangesAsync` batch after it succeeds — no database transaction is needed to "roll back" a call to Stripe's API (that already happened or didn't; a local DB transaction can't undo it), so the guarantee has to come from ordering, not from `TransactionScope`. This mirrors `HandleStripeWebhookCommandHandler`'s existing refund path (Story 4.6): call `CreateRefundAsync` first, only write audit/state afterward.

### Why `IssueReturnRefundCommand` takes no amount parameter

AC #2 says "the original payment amount" — an admin-supplied amount would be a partial-refund feature this story's AC doesn't ask for and would reopen the "never trust a client-sent amount" principle this codebase has enforced since Story 4.5 (cart total) and 4.6 (webhook payment amount). `Order.TotalInCents` is the only source of truth used here.

## Project Structure Notes

New `Application/Returns/Commands/UpdateReturnStatusCommand.cs`, `IssueReturnRefundCommand.cs`, `Domain/Events/ReturnStatusUpdatedEvent.cs`, `Application/Returns/EventHandlers/ReturnStatusUpdatedEmailHandler.cs`, `Application/Common/Exceptions/RefundFailedException.cs`, `Application/Common/OrderNumberFormatter.cs`, `Web/Endpoints/AdminReturns.cs`. Modified: `Domain/Entities/PaymentAuditLog.cs` (+migration), `Domain/Events/RefundIssuedEvent.cs`, `Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs`, `Infrastructure/Identity/AccountService.cs` (label fix + shared formatter).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 5.3 acceptance criteria (Epic 5 section, line ~943)
- `_bmad-output/implementation-artifacts/5-1-demande-de-retour-client.md` — `Return`/`ReturnStatus` entity this story transitions; `ReturnRequestedEvent` precedent for this epic's email-handler shape
- `_bmad-output/implementation-artifacts/5-2-notifications-email-changements-de-statut.md` — the `Administrator` role/`AuthorizationBehaviour` pattern, `AdminOrders.cs` endpoint-group precedent this story's `AdminReturns.cs` mirrors
- `_bmad-output/implementation-artifacts/4-6-confirmation-commande-et-anti-overselling.md` — `PaymentAuditLog`, `IPaymentService.CreateRefundAsync`, `Order.StripePaymentIntentId`, and the explicit note that `RefundIssuedEvent` was deliberately left for this story
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs`, `Domain/Events/RefundIssuedEvent.cs` — existing, extended (not replaced) this story

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 warnings, 0 errors. `dotnet test MonEcommerce.sln` — 220/220 passed. New migration `AddPaymentAuditLogAdminRefundFields` generated cleanly.
- Two test-authoring bugs (mine, not product code) caught by the first `dotnet test` run: both `IssueReturnRefundCommandHandlerTests` tests initially queried a just-`Add`ed-but-not-yet-`SaveChangesAsync`'d entity — the InMemory provider doesn't surface unsaved entities to a fresh query (same class of mistake, and same fix, as a bug caught during Story 5.1's own test-writing). Fixed by saving before querying.
- Found an existing-but-unused test file that needed updating for the new parameter: `RefundIssuedEmailHandlerTests.cs` (built ahead of schedule alongside the never-until-now-used `RefundIssuedEmailHandler`, broke on the new `OrderNumber` constructor parameter) — updated its assertions to check the AC #4 content: order number format, amount, and the "3 à 5 jours ouvrés" text, not just presence of a raw Guid.
- No frontend/mobile work this story — same reasoning as Story 5.2: these are admin-only endpoints with no UI consumer built yet (Epic 7's job), and the customer-facing side is entirely emails.

### Completion Notes List

- `RefundIssuedEvent`/`RefundIssuedEmailHandler` (built ahead of schedule, Story 4.6 explicitly deferred using it to this story) now get their first real use: extended with `OrderNumber` and the email body updated for AC #4's exact content requirements (amount, order number, "3 à 5 jours ouvrés" processing time).
- `PaymentAuditLog` (Story 4.6) extended with `AdminUserId`/`StripeRefundId` rather than creating a second audit table — AC #5's "complete audit trail: amount, date, admin user, Stripe refund ID" fits directly into the existing "one row per payment-related outcome" table, with both new fields staying null on the webhook-originated rows that don't apply to.
- `IssueReturnRefundCommandHandler` guarantees AC #3's "no partial state persisted" by ordering, not a database transaction: the Stripe refund call happens before any tracked-entity mutation or `SaveChangesAsync`, so a failure there literally cannot leave a half-written row (there's nothing to roll back — nothing was ever staged). New `RefundFailedException` → 502 Bad Gateway.
- Extracted `OrderNumberFormatter` and `ReturnStatusLabelFormatter` out of `AccountService` (both were `private static`, now shared in `Application/Common/`) since this story needed the exact same formatting logic in a second, unrelated place (the new commands/events) — `AccountService` itself now calls the shared versions, no behavior change there.
- Fixed a wording mismatch surfaced by reading this story's AC closely: Story 5.1's `ReturnStatus.Approved` French label was "Approuvé"; this story's AC literally says "Validé" for the same concept — corrected in the shared formatter (and by extension, the customer-facing order-detail page, which reads the same label).
- `UpdateReturnStatusCommand`/`IssueReturnRefundCommand` are the second and third commands in this codebase (after Story 5.2's `UpdateOrderStatusCommand`) to use `[Authorize(Roles = Roles.Administrator)]` — added matching rejection-path coverage to the existing `AuthorizationPipelineTests.cs`.

### File List

**Backend**
- `backend/MonEcommerce/src/Domain/Entities/PaymentAuditLog.cs` (modified — `AdminUserId`, `StripeRefundId`)
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/PaymentAuditLogConfiguration.cs` (modified)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260731002936_AddPaymentAuditLogAdminRefundFields.cs` + `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` (new/modified)
- `backend/MonEcommerce/src/Domain/Events/RefundIssuedEvent.cs` (modified — `OrderNumber`), `ReturnStatusUpdatedEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs` (modified), `ReturnStatusUpdatedEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/Commands/UpdateReturnStatusCommand.cs`, `UpdateReturnStatusCommandHandler.cs`, `UpdateReturnStatusCommandValidator.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/Commands/IssueReturnRefundCommand.cs`, `IssueReturnRefundCommandHandler.cs`, `IssueReturnRefundCommandValidator.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Exceptions/RefundFailedException.cs` (new)
- `backend/MonEcommerce/src/Application/Common/OrderNumberFormatter.cs`, `ReturnStatusLabelFormatter.cs` (new)
- `backend/MonEcommerce/src/Web/Endpoints/AdminReturns.cs` (new)
- `backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (modified — 502 mapping)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (modified — shared formatters, "Validé" label)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Commands/UpdateReturnStatusCommandHandlerTests.cs`, `IssueReturnRefundCommandHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/EventHandlers/RefundIssuedEmailHandlerTests.cs` (modified), `ReturnStatusUpdatedEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/AuthorizationPipelineTests.cs` (modified)
