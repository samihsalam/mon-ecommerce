# Story 7.3: Traitement des Demandes de Retour

Status: done

## Story

As an administrator,
I want to view and process return requests (approve or reject) with Stripe refund,
so that the customer return cycle is closed efficiently.

## Acceptance Criteria

1. Given an admin accesses the returns list, when `GET /api/v1/admin/returns` is called, then a list of return requests is returned with: order number, customer, reason, date, status, photos.
2. Given a pending return request, when `PATCH /api/v1/admin/returns/{id}` is called with `{ status: "Validé" }`, then the return is approved and the customer is notified by email. **Already implemented (Story 5.3)**.
3. Given a validated return, when `POST /api/v1/admin/returns/{id}/refund` is called, then the Stripe Refund API is called for the original payment amount, and `RefundIssuedEvent` is published → customer refund confirmation email within ≤ 30 seconds. **Already fully implemented (Story 5.3)** — no changes needed.
4. Given a return is rejected, when `PATCH /api/v1/admin/returns/{id}` is called with `{ status: "Refusé", reason: "..." }`, then the customer is notified by email with the rejection reason. **Partially implemented (Story 5.3)** — the endpoint/status transition exists, but there was no `reason` field anywhere in the command, event, or email.
5. Refund audit trail is stored: amount, Stripe refund ID, admin user, timestamp. **Already fully implemented (Story 5.3's `PaymentAuditLog`)** — no changes needed.
6. Returns can be filtered by status (En attente / Validé / Refusé) and date.

## Tasks / Subtasks

- [x] Task 1: Extract `Application/Common/ReturnReasonLabelFormatter.cs` from `AccountService.MapReturnReasonLabel` (needed in a second place — this story's admin list — same refactor precedent as `OrderStatusLabelFormatter`, Story 7.1). `AccountService.MapReturnReasonLabel` becomes a thin delegate.
- [x] Task 2: `UpdateReturnStatusCommand` gains a `string? Reason` parameter (AC #4). Validator: required when `NewStatus == Rejected` ("Un motif est requis pour refuser une demande de retour."), untouched when approving. `ReturnStatusUpdatedEvent` gains `string? Reason`; `ReturnStatusUpdatedEmailHandler` includes it in the email body (HTML-encoded — free-text admin input reflected into an email, same XSS-prevention convention as `ReturnRequestedEmailHandler`, Story 5.1) only when present, so the approval-path email is unchanged. **Deliberately not persisted anywhere** (no new `Return.RejectionReason` column) — there is no explicit "logged" requirement for rejection reasons the way Story 7.2's AC #4 explicitly demanded for order status changes; adding a persisted audit trail here would be scope not asked for. See Dev Notes.
- [x] Task 3: `Application/Returns/Models/{AdminReturnSummaryDto,AdminReturnFilter}.cs`, `Application/Common/Interfaces/IAdminReturnService.cs` + `Infrastructure/Returns/AdminReturnService.cs` (AC #1, #6) — same "Infrastructure service does the EF Core query + batched customer-name resolution via `UserManager.Users`, Application handler delegates" pattern as Story 7.1's `AdminOrderService`. Returns a plain `List<T>`, not a paginated result — AC #1 says "a list of return requests," not "a paginated list" (contrast with Story 7.1's AC #1, which explicitly says "paginated"); no pagination added for something not asked for, same restraint as Story 6.4's `GetStockHistoryQuery`.
- [x] Task 4: `Application/Returns/Queries/GetAdminReturnsQuery.cs` + Handler + Validator (AC #1, #6). `[Authorize(Roles = Roles.Administrator)]`. Filter by `Status` and a `DateFrom`/`DateTo` range — AC #6 just says "date" (singular), interpreted as a range for the same reason and same convention as Story 7.1's order list (a single exact date is a far less useful filter for an admin list than a range).
- [x] Task 5: `Web/Endpoints/AdminReturns.cs` — `GET ""` mapped to `GetAdminReturns` (unique method name across every endpoint group — same lesson as Story 7.1's `GetAdminOrders`). `UpdateReturnStatusRequest` gains `Reason`.
- [x] Task 6: Unit tests — extend `UpdateReturnStatusCommandHandlerTests` (reason threaded into the event, rejection without a reason fails validation), extend `ReturnStatusUpdatedEmailHandlerTests` (reason included/HTML-encoded when present, omitted when absent), new `AdminReturnServiceTests` (status filter, date range filter, order number/customer name/reason label resolution, no-N+1 name resolution), `GetAdminReturnsQueryValidatorTests`.

## Dev Notes

### Rejection reason is emailed, not persisted

AC #4 requires the customer be notified by email with the rejection reason — it does not say the reason must be stored anywhere queryable afterward, unlike Story 7.2's AC #4, which explicitly demanded a full audit log (previous status, new status, admin, timestamp) for order status changes. Adding a `Return.RejectionReason` column (+ migration) here would be solving a problem this AC doesn't actually state, however reasonable it might feel in isolation — flagged as a deliberate, discussable scope boundary rather than silently done or silently skipped. An admin who wants to know why a specific return was rejected after the fact would need to check the email that was sent, or a future story would need to add persistence explicitly.

### Why `GetAdminReturnsQuery` returns a plain list, not a paginated result

AC #1's own wording ("a list of return requests is returned with: ...") doesn't say "paginated," in contrast to Story 7.1's AC #1 for orders, which explicitly does. Following that literal distinction rather than assuming every admin list needs identical shape — same restraint already applied in Story 6.4's `GetStockHistoryQuery` (also an unpaginated list, also for a bounded-in-practice admin table).

### AC #6's "date" (singular) interpreted as a range

Same reasoning as Story 7.1's Dev Notes for the near-identical AC #2 orders-list wording: a single exact date is a far less useful admin filter than a range, and this codebase already has an established `dateFrom`/`dateTo` convention (`AdminOrderFilter`) to be consistent with.

## Project Structure Notes

New: `Application/Common/ReturnReasonLabelFormatter.cs`, `Application/Returns/Models/{AdminReturnSummaryDto,AdminReturnFilter}.cs`, `Application/Common/Interfaces/IAdminReturnService.cs`, `Infrastructure/Returns/AdminReturnService.cs`, `Application/Returns/Queries/GetAdminReturnsQuery.cs` (+ Handler + Validator), unit tests under `tests/Application.UnitTests/Returns/`. Modified: `Application/Returns/Commands/UpdateReturnStatusCommand.cs` (+ Handler + Validator), `Domain/Events/ReturnStatusUpdatedEvent.cs`, `Application/Returns/EventHandlers/ReturnStatusUpdatedEmailHandler.cs`, `Infrastructure/Identity/AccountService.cs` (delegates to the extracted formatter), `Infrastructure/DependencyInjection.cs` (`IAdminReturnService` registration), `Web/Endpoints/AdminReturns.cs`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 7.3 acceptance criteria (Epic 7 section, line ~1186).
- `_bmad-output/implementation-artifacts/7-1-liste-et-filtrage-des-commandes.md` — the `IAdminOrderService`/batched-name-resolution/date-range-filter pattern this story's `AdminReturnService` follows directly.
- `_bmad-output/implementation-artifacts/5-3-remboursement-admin-et-confirmation-client.md` — where AC #3/#5 (Stripe refund + audit trail) were already fully implemented; verified, not rebuilt.
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` — the `WebUtility.HtmlEncode` convention for admin/customer free text reflected into an email, reused for the rejection reason.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `Reason` was added as an optional, defaulted parameter on both `UpdateReturnStatusCommand` and `ReturnStatusUpdatedEvent` specifically so every pre-existing call site (Story 5.3's approval-path tests, the handler's own approve/reject flow) kept compiling unchanged — only the new rejection-path behavior needed new tests, nothing pre-existing needed updating for compile-correctness.
- One new test initially failed: asserting `body.Contains("Produit porté.")` against the HTML-encoded email body — `WebUtility.HtmlEncode` encodes accented characters too (not just `<script>`-style markup), so "é" became `&#233;` in the rendered output. Fixed by using an ASCII-only reason for that content-assertion test; the encoding behavior itself is covered separately by a dedicated `<script>`-injection test.

### Completion Notes List

- `GET /api/v1/admin/returns` implemented (AC #1, #6) via `IAdminReturnService`/`AdminReturnService`, the same "Infrastructure service + batched `UserManager.Users` name resolution" pattern as Story 7.1's `AdminOrderService`. Returns a plain list (no pagination) and supports status + date-range filtering.
- `ReturnReasonLabelFormatter` extracted from `AccountService.MapReturnReasonLabel` (now a one-line delegate) — reused by the admin list so both customer-facing and admin-facing return-reason labels stay a single source of truth.
- AC #4's rejection reason now flows end-to-end: `UpdateReturnStatusCommand` → validated as required when rejecting → `ReturnStatusUpdatedEvent` → HTML-encoded into the customer email, omitted entirely (not even an empty "Motif:" line) when approving.
- AC #2, #3, #5 (approval flow, Stripe refund, refund audit trail) were already fully implemented by Story 5.3 — verified, not rebuilt; `IssueReturnRefundCommandHandler` and `PaymentAuditLog` untouched by this story.
- Rejection reason is deliberately not persisted anywhere (no new `Return` column) — flagged in Dev Notes as a scope boundary, not silently decided.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 360/360 Application.UnitTests passing, including 17 new tests across handler, validator, email-handler, and the new `AdminReturnServiceTests`/`GetAdminReturnsQueryValidatorTests`. No migration needed — no schema changes. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Application/Common/ReturnReasonLabelFormatter.cs`
- `backend/MonEcommerce/src/Application/Returns/Models/{AdminReturnSummaryDto,AdminReturnFilter}.cs`
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAdminReturnService.cs`
- `backend/MonEcommerce/src/Infrastructure/Returns/AdminReturnService.cs`
- `backend/MonEcommerce/src/Application/Returns/Queries/GetAdminReturnsQuery.cs` (+ Handler, Validator)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Commands/UpdateReturnStatusCommandValidatorTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Services/AdminReturnServiceTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Queries/GetAdminReturnsQueryValidatorTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Application/Returns/Commands/UpdateReturnStatusCommand.cs` (+ Handler, Validator — `Reason`)
- `backend/MonEcommerce/src/Domain/Events/ReturnStatusUpdatedEvent.cs` (`Reason`)
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnStatusUpdatedEmailHandler.cs` (includes reason when present)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (`MapReturnReasonLabel` delegates to the extracted formatter)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (`IAdminReturnService` registration)
- `backend/MonEcommerce/src/Web/Endpoints/AdminReturns.cs` (`GET` root route, `Reason` on the update request)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Commands/UpdateReturnStatusCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/EventHandlers/ReturnStatusUpdatedEmailHandlerTests.cs`
