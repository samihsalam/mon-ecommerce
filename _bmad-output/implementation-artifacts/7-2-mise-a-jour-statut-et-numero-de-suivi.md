# Story 7.2: Mise à Jour Statut & Numéro de Suivi

Status: done

## Story

As an administrator,
I want to update an order's status and enter a tracking number,
so that customers are informed and shipment traceability is maintained.

## Acceptance Criteria

1. Given an order in "En préparation" status, when `PATCH /api/v1/admin/orders/{id}/status` is called with `{ status: "Expédiée", trackingNumber: "..." }`, then the status is updated and `OrderShippedEvent` is published → customer email sent within ≤ 30 seconds. **Already implemented (Story 5.2)** — this story adds the missing transition/audit rules around it.
2. Given a status transition to "Expédiée" without a tracking number, when the request is processed, then a `422` error is returned: "Le numéro de suivi est requis pour le statut Expédiée".
3. Given an invalid status transition (e.g., "Livrée" → "En préparation"), when the request is processed, then a `422` error is returned with the list of valid transitions.
4. Every status change is logged: previous status, new status, admin user ID, timestamp.
5. Valid transitions are: En attente → En préparation → Expédiée → Livrée.
6. Cancellation is only possible from "En attente" or "En préparation".

## Tasks / Subtasks

- [x] Task 1: Fix `OrderStatusLabelFormatter` — `OrderStatus.Pending` now maps to "En attente" (was "En préparation", same as `Processing`). AC #5's own transition list names four *distinct* stages ("En attente → En préparation → ..."), but the formatter (Story 2.5) collapsed `Pending`+`Processing` into one shared label — a real, pre-existing inconsistency this story's own transition rules expose and correct. Customer-facing effect: `Account`'s order history/detail now shows "En attente" for a brand-new order instead of "En préparation" until it's actually picked up — a more accurate label, not a regression. See Dev Notes.
- [x] Task 2: `Domain/Entities/OrderStatusHistory.cs` (`BaseAuditableEntity` — free `Created`/`CreatedBy` for AC #4's "admin user ID, timestamp", same convention as `StockMovement`/`PaymentAuditLog`/`EmailDispatchLog`): `OrderId`, `PreviousStatus`, `NewStatus`. `Infrastructure/Data/Configurations/OrderStatusHistoryConfiguration.cs` + migration. `IApplicationDbContext`/`ApplicationDbContext` gain `DbSet<OrderStatusHistory> OrderStatusHistories`. No new `GET` endpoint to read this history back — not asked for by any AC bullet (AC #4 is a write-side requirement only), same precedent as Story 5.4's `EmailDispatchLog` (logged, never queried back by an endpoint either).
- [x] Task 3: Extend `UpdateOrderStatusCommandHandler` (AC #2, #3, #5, #6) — reusing, not replacing, Story 5.2's existing command/handler, exactly as its own comment already anticipated ("Story 7.2 owns the real admin UI/workflow built on top of this same command"). A `Dictionary<OrderStatus, OrderStatus[]>` transition map (`Pending → [Processing, Cancelled]`, `Processing → [Shipped, Cancelled]`, `Shipped → [Delivered]`, `Delivered → []`, `Cancelled → []`) — strict "next stage only," no skipping ahead, no staying at the same status, no going backward; AC #6's cancellation rule falls directly out of this map (only `Pending`/`Processing` list `Cancelled` as valid). An invalid transition throws `ValidationException` (422) naming the attempted transition and listing the valid next statuses from the order's current status (empty for the two terminal states). Writes one `OrderStatusHistory` row per successful transition.
- [x] Task 4: Align `UpdateOrderStatusCommandValidator`'s tracking-number-required message with AC #2's exact wording ("Le numéro de suivi est requis pour le statut Expédiée" — was "...pour marquer une commande comme expédiée.").
- [x] Task 5: Unit tests — extend `UpdateOrderStatusCommandHandlerTests` (every valid forward transition, cancellation from Pending/Processing, invalid/backward/skip-ahead transitions rejected with the valid-transitions list, terminal-state transitions rejected, one `OrderStatusHistory` row written per successful change with correct previous/new status and `CreatedBy`), validator test for the exact AC #2 message. Update `AccountServiceOrdersTests`/`AccountServiceTests`/`OrderStatusLabelFormatter`-adjacent tests if any assert `Pending` → "En préparation" (verified: none do — only `ReturnStatus.Pending` → "En attente" is asserted anywhere today, an unrelated enum).

## Dev Notes

### Fixing `OrderStatusLabelFormatter`'s Pending/Processing collapse, not just adding transition rules on top of it

Story 2.5's Dev Notes justified merging `Pending` and `Processing` into one "En préparation" label as intentional simplification for customers ("4 labels for 5 values"). Story 7.2's own AC #5 directly contradicts that premise: it names "En attente" as the *first* stage and "En préparation" as a *distinct second* stage — i.e., the PRD always intended these to be two different, customer-visible states, not one merged label. Since this story is the one literally building the state machine those two labels are supposed to represent, leaving the old merged label in place while writing precise transition logic against the underlying 5-value enum would be internally inconsistent (the admin's transition error messages would say "En attente → En préparation" while the customer's own order page still showed "En préparation" for that exact same order). Fixed at the source (`OrderStatusLabelFormatter`), which `AccountService`'s customer-facing order history already consumes — verified no existing test asserts the old `Pending` → "En préparation" mapping specifically (grepped for every "En préparation"/"En attente" occurrence across `src/`/`tests/`), so this is a safe, unambiguous correction rather than a breaking one. Not verified against the Angular frontend (out of scope for this backend story) — flagged as a possible follow-up if the frontend independently hardcodes the old label anywhere.

### Why the transition map lives inline in the handler, not a shared/extracted helper

Nothing else in this codebase needs the order-status transition graph yet (unlike `OrderNumberFormatter`/`OrderStatusLabelFormatter`, which were extracted only once a second real caller existed — Stories 5.3, 7.1). Extracting it now for a single caller would be speculative, not YAGNI.

### No `GET` endpoint for `OrderStatusHistory`

AC #4 only requires that changes *are logged*, not that they're retrievable through a new endpoint — same shape as Story 5.4's `EmailDispatchLog`, which this codebase has never exposed through a `GET` either. Not added here; would be a reasonable, low-risk follow-up if a future story needs it.

## Project Structure Notes

New: `Domain/Entities/OrderStatusHistory.cs`, `Infrastructure/Data/Configurations/OrderStatusHistoryConfiguration.cs` (+ migration). Modified: `Application/Common/OrderStatusLabelFormatter.cs`, `Application/Common/Interfaces/IApplicationDbContext.cs`, `Infrastructure/Data/ApplicationDbContext.cs`, `Application/Orders/Commands/UpdateOrderStatusCommandHandler.cs`, `Application/Orders/Commands/UpdateOrderStatusCommandValidator.cs`, `tests/Application.UnitTests/Orders/Commands/UpdateOrderStatusCommandHandlerTests.cs` (pre-existing from Story 5.2, extended here).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 7.2 acceptance criteria (Epic 7 section, line ~1163).
- `backend/MonEcommerce/src/Application/Orders/Commands/UpdateOrderStatusCommand.cs` — Story 5.2's own comment naming this story as the intended owner of the full workflow.
- `_bmad-output/implementation-artifacts/6-4-gestion-des-stocks-et-alertes.md` — `StockMovement`'s "`BaseAuditableEntity` gives admin+timestamp for free, one row per change" convention this story's `OrderStatusHistory` follows exactly.
- `backend/MonEcommerce/src/Application/Common/OrderStatusLabelFormatter.cs`, `src/Infrastructure/Identity/AccountService.cs` — the label mapping corrected here, and its one other consumer.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Discovered the `Pending`/`Processing` label collision while cross-referencing this story's own AC #5 wording against `OrderStatusLabelFormatter`'s existing mapping — the AC's transition graph names four distinct stages, but the formatter had merged two of them since Story 2.5. Grepped every `"En préparation"`/`"En attente"` occurrence across `src/`/`tests/` before changing it, to confirm no existing test asserted the old `Pending` → "En préparation" mapping specifically (none did).
- The pre-existing `UpdateOrderStatusCommandHandlerTests`' first test seeded a `Pending` order and transitioned it directly to `Shipped` — valid under the old (nonexistent) transition rules, invalid now (skips the `Processing` stage). Updated its seed to `Processing`, matching what AC #5's own sequence actually allows.

### Completion Notes List

- Extended (not replaced) Story 5.2's existing `UpdateOrderStatusCommand`/Handler/Validator, exactly as that story's own comment anticipated ("Story 7.2 owns the real admin UI/workflow built on top of this same command") — `PATCH /api/v1/admin/orders/{id}/status` itself needed no endpoint changes.
- Added a `Dictionary<OrderStatus, OrderStatus[]>` transition map enforcing AC #3/#5/#6: strict next-stage-only progression (`Pending`→`Processing`→`Shipped`→`Delivered`), cancellation only from `Pending`/`Processing`, no skipping, no going backward, no same-status "transitions." An invalid attempt throws `ValidationException` (422) naming the attempted transition and listing every valid next status from the order's current one (or stating none exist, for the two terminal states).
- `OrderStatusHistory` (new entity, migration `AddOrderStatusHistory`) logs one row per successful transition — `BaseAuditableEntity` gives AC #4's "admin user ID, timestamp" for free, same convention as `StockMovement` (Story 6.4). No new `GET` endpoint added to read it back — not required by any AC bullet, same precedent as `EmailDispatchLog`.
- Fixed `OrderStatusLabelFormatter`: `Pending` now maps to "En attente" (previously merged with `Processing`'s "En préparation") — a real correction to align with this story's own AC #5 wording, not a new label invented for this story. This is also customer-facing (`AccountService`'s order history/detail uses the same formatter) — verified no existing test depended on the old mapping before changing it; added a new test locking in the corrected label.
- Aligned the tracking-number-required validation message with AC #2's exact French wording (dropped the trailing period and rephrased to match verbatim).
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 343/343 Application.UnitTests passing, including 14 new/changed tests (transition matrix via `[TestCase]`, audit-row assertions, rejected-transition-leaves-nothing-mutated, the corrected label). `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test/migrate on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Domain/Entities/OrderStatusHistory.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/OrderStatusHistoryConfiguration.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260802012653_AddOrderStatusHistory.cs` (+ `.Designer.cs`, snapshot update)

**Modified:**
- `backend/MonEcommerce/src/Application/Common/OrderStatusLabelFormatter.cs` (`Pending` → "En attente")
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (`OrderStatusHistories` DbSet)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (`OrderStatusHistories` DbSet)
- `backend/MonEcommerce/src/Application/Orders/Commands/UpdateOrderStatusCommandHandler.cs` (transition validation + audit logging)
- `backend/MonEcommerce/src/Application/Orders/Commands/UpdateOrderStatusCommandValidator.cs` (AC #2 exact message)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/Commands/UpdateOrderStatusCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/Commands/UpdateOrderStatusCommandValidatorTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceOrdersTests.cs` (new label regression test)
