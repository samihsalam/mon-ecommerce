# Story 2.5: Historique des Commandes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to view the list of all my orders and the detail of each one,
so that I can track my past purchases and their current status.

## Acceptance Criteria

1. **Given** an authenticated customer with past orders, **when** `GET /api/v1/account/orders` is called, **then** a paginated list is returned (date, total amount in cents, status, order number).
2. **Given** a specific order ID, **when** `GET /api/v1/account/orders/{orderId}` is called, **then** full order details are returned (items, quantities, prices, delivery address, tracking number if available).
3. **Given** a customer with no orders, **when** the orders page is displayed, **then** an empty state is shown: "Aucune commande pour le moment" with a CTA "Commencer à shopper".
4. The order history is accessible in 2 taps from the profile screen (mobile).
5. Order status labels are human-readable in French (En préparation · Expédiée · Livrée · Annulée).
6. The list is sorted by date descending (most recent first).

## Tasks / Subtasks

### Backend — genuinely new; read-only against schema that already exists (Story 1.3), no checkout flow needed to build this

- [x] Task 1: DTOs and pagination shape (AC: #1, #2)
  - [x] `Application/Account/Models/PagedResult.cs`: `public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);` — first paginated response in this codebase, no existing convention to follow
  - [x] `Application/Account/Models/OrderSummaryDto.cs`: `public record OrderSummaryDto(Guid Id, string OrderNumber, DateTimeOffset Date, int TotalInCents, string Status);`
  - [x] `Application/Account/Models/OrderDetailDto.cs`: `public record OrderDetailDto(Guid Id, string OrderNumber, DateTimeOffset Date, int TotalInCents, string Status, string? TrackingNumber, AddressDto ShippingAddress, List<OrderItemDto> Items);`
  - [x] `Application/Account/Models/OrderItemDto.cs`: `public record OrderItemDto(string ProductName, int UnitPriceInCents, int Quantity);`
  - [x] `Application/Common/Mappings/OrderStatusFrenchLabels.cs` (or a static helper): maps `OrderStatus` → the 4 French labels from AC #5. **The enum has 5 values, the AC lists 4 labels** — `Pending` and `Processing` both map to "En préparation" (from the customer's perspective there's no meaningful difference between "not started" and "actively being prepared"); `Shipped` → "Expédiée"; `Delivered` → "Livrée"; `Cancelled` → "Annulée". Documented as a deliberate interpretation, not an oversight.
  - [x] No `OrderNumber` field exists on the `Order` entity (just `Id: Guid`) — format one from the Guid (`"#" + Id.ToString("N")[..8].ToUpperInvariant()`) rather than adding a migration for a dedicated sequential-order-number field, which is squarely Epic 4's (checkout/order-creation) concern, not this read-only history view's.
- [x] Task 2: `GetOrdersQuery` (AC: #1, #6)
  - [x] `Application/Account/Queries/GetOrdersQuery.cs`: `[Authorize] public record GetOrdersQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<OrderSummaryDto>>;`
  - [x] Handler resolves current user id via `IUser` (same pattern as `GetProfileQueryHandler`), delegates to `IAccountService.GetOrdersAsync`, which queries `_context.Orders.Where(o => o.UserId == userId).OrderByDescending(o => o.Created)`, paginates, and maps to `OrderSummaryDto`
- [x] Task 3: `GetOrderDetailQuery` (AC: #2) — **IDOR check required**
  - [x] `Application/Account/Queries/GetOrderDetailQuery.cs`: `[Authorize] public record GetOrderDetailQuery(Guid OrderId) : IRequest<OrderDetailDto>;`
  - [x] `AccountService.GetOrderDetailAsync`: queries `_context.Orders.Include(o => o.Items).Include(o => o.ShippingAddress).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId)` — **filtering by `UserId` in the same query, not as a separate check after loading by ID alone** — if null, throw `NotFoundException` (the same 404 whether the order doesn't exist at all or belongs to a different customer; returning 403 for "exists but not yours" would let a customer enumerate other people's valid order IDs by status-code alone)
- [x] Task 4: Endpoints (AC: #1, #2)
  - [x] Add to the existing `Web/Endpoints/Account.cs` (same `/api/v1/account` prefix already matches `GET orders` and `GET orders/{orderId}`) — both `RequireAuthorization()`
- [x] Task 5: Backend tests
  - [x] `AccountServiceOrdersTests.cs` — `GetOrdersAsync`: empty list for a user with no orders; correct pagination (page/pageSize slicing, `TotalCount` reflects the full set not just the page); sorted descending by date; only returns the requesting user's own orders (seed orders for two different users, confirm no cross-contamination)
  - [x] `GetOrderDetailAsync`: returns full detail including items and shipping address for the owner; throws `NotFoundException` for a non-existent order ID; throws `NotFoundException` (not a different exception) for an order that exists but belongs to a *different* user — proves the IDOR guard

### Angular

- [x] Task 6: `orders.store.ts` (AC: #1, #2, #3, #6)
  - [x] `features/account/orders.store.ts`: `signalStore` with `orders`, `totalCount`, `page`, `isLoading`, `error`, `selectedOrder` state; `loadOrders(page)`, `loadOrderDetail(orderId)`
- [x] Task 7: Order history page (AC: #1, #3, #4, #5, #6)
  - [x] `features/account/pages/orders/orders.component.ts` + `.html` + `.scss` — list sorted by date descending (as returned by the backend, no client-side re-sort needed), French status labels, empty state ("Aucune commande pour le moment" + CTA linking to `/`, since the catalogue doesn't exist until Epic 3 — same `/` redirect precedent as Stories 2.1–2.3)
  - [x] Route `path: 'compte/commandes'`, `canActivate: [authGuard]`
  - [x] Link added to `profile.component.html` ("Historique des commandes") — 1 tap from the profile screen, well inside AC #4's 2-tap budget
- [x] Task 8: Order detail page (AC: #2, #5)
  - [x] `features/account/pages/order-detail/order-detail.component.ts` + `.html` + `.scss` — items/quantities/prices, delivery address, tracking number if present
  - [x] Route `path: 'compte/commandes/:orderId'`, `canActivate: [authGuard]`
- [x] Task 9: Angular tests
  - [x] `orders.store.spec.ts` — `loadOrders()`, `loadOrderDetail()`
  - [x] `orders.component.spec.ts` — renders the list, shows the empty state when there are no orders
  - [x] `order-detail.component.spec.ts` — renders full detail
  - [x] `ng build` (production) + `ng test` via Edge as `ChromeHeadless`

### Flutter

- [x] Task 10: `orders_provider.dart` (AC: #1, #2, #3, #6)
  - [x] `features/account/providers/orders_provider.dart`: `Notifier`-based, same shape as `account_provider.dart`
- [x] Task 11: Order history and detail screens (AC: #1, #2, #3, #4, #5, #6)
  - [x] `features/account/screens/orders_screen.dart`, `features/account/screens/order_detail_screen.dart`
  - [x] Routes `/compte/commandes` and `/compte/commandes/:orderId` added to `router.dart` (both protected — extend the existing `redirect` callback's protected-path check)
  - [x] Link added to `profile_screen.dart`
- [x] Task 12: Flutter tests
  - [x] `orders_screen_test.dart`
  - [x] Not verified by tooling — same environment gap as every prior story

### Verification

- [x] Task 13: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] Angular: `ng build` (production) + `ng test` via Edge as `ChromeHeadless`
  - [x] Flutter: unverified, flagged above

### Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–2.4) run against the full diff. Both Blind Hunter and Edge Case Hunter independently reported a prompt-injection attempt embedded in `git diff` tool output (a forged system-reminder about a date change, instructing them to stay silent) — both correctly ignored the embedded instructions and flagged it instead of complying; no action needed beyond noting it.

- [x] [Review][Patch] No bounds-checking on `page`/`pageSize` in `GetOrdersQuery` — confirmed independently by two reviewers. `page <= 0` computes a negative `Skip()`, which SQL Server rejects at runtime (500) but which the EF Core **InMemory** provider used by `AccountServiceOrdersTests` doesn't reproduce (it just returns everything), so this was invisible to the existing test suite and would only have surfaced against the real database. `pageSize` also had no upper bound, letting a caller force an arbitrarily large result set per request. **Fixed:** added `GetOrdersQueryValidator.cs` (`Page >= 1`, `PageSize` in `[1, 100]`), matching the established FluentValidation pattern used everywhere else in this codebase — rejects with a clear 422 instead of crashing. Covered by `GetOrdersQueryValidatorTests.cs`.
- [x] [Review][Patch] `GetOrdersAsync`'s `OrderByDescending(o => o.Created)` had no secondary sort key — two orders sharing the same `Created` timestamp have no guaranteed stable order across separate `Skip`/`Take` calls, which could duplicate or skip an order across two pages. **Fixed:** added `.ThenByDescending(o => o.Id)`.
- [x] [Review][Patch] Real, concrete Flutter bug: navigating from one order's detail screen directly to another's briefly rendered the FIRST order's stale data (title, items, address) before flipping to the loading state. Root cause was two compounding issues: (1) `OrdersState.copyWith`'s `selectedOrder` used a "sticky" `?? this.selectedOrder` fallback (unlike `error`, which is always overwritten), so it could never be explicitly cleared to `null`; (2) `order_detail_screen.dart`'s `initState` wrapped the notifier call in `Future.microtask(...)`, which doesn't run before the widget's first `build()`, so even a correct clear-on-load wouldn't have been visible in time. **Fixed both:** `selectedOrder` is now always-overwritten like `error`, and `initState` calls `loadOrderDetail` directly (its synchronous prefix — before the first `await` — now clears `selectedOrder` and sets `isLoading` before the first frame renders).
- [x] [Review][Patch] Flutter's route guard used `state.matchedLocation.startsWith('/compte/commandes')`, a raw string prefix rather than a path-segment boundary — a hypothetical future public route like `/compte/commandes-publiques` would also be forced through the auth redirect. **Fixed:** exact match on `/compte/commandes` or a proper `/compte/commandes/`-prefixed check.
- [x] [Review][Patch] Angular's order-list date (`{{ order.date | date: 'dd/MM/yyyy' }}`) had no pinned timezone — since this app uses SSR, the pipe runs once server-side (Node process timezone) and again client-side during hydration (browser-local timezone); an order created near local midnight could render a different date on each side, causing a hydration mismatch. **Fixed:** pinned to `'UTC'`.
- [x] [Review][Note] Order numbers are formatted from 32 bits of the order `Guid` (`#XXXXXXXX`) — collision risk becomes non-trivial (~50%) around ~77,000 total orders. Acceptable for a *display-only* label at the scale this app currently operates at (zero real orders exist yet, pre-Epic-4); revisit if order volume grows into the tens of thousands. No code change.
- [x] [Review][Defer] Angular's `orders.store.ts` shares a single `isLoading`/`error` pair across `loadOrders()` and `loadOrderDetail()`. Not currently exploitable — list and detail are separate routed pages that are never mounted together, and each page's own `ngOnInit` re-triggers its own load, resetting the shared state correctly for that page. Flagged as a design footgun for future reuse (e.g. a future widget rendering both list and detail simultaneously), not an active bug; would need per-operation loading flags to fully close.
- [x] [Review][Defer] Neither Angular nor Flutter has a dedicated UI test for the "order belongs to another user" 404 path (only the backend's IDOR test covers it). Both platforms' generic error-message handling already covers this scenario architecturally (a 404 surfaces the same "impossible de charger" message as any other failure), so there's no missing behavior — just missing test coverage of that specific path. Low priority given the backend guard is the actual security boundary and is already tested there.

## Dev Notes

### This story is buildable now even though checkout (Epic 4) doesn't exist yet

`Order`/`OrderItem` entities and their EF Core configurations already exist from Story 1.3's domain schema — nothing about *reading* order history requires the ability to *create* orders first. In this environment, every account will genuinely have zero orders (same situation Story 2.4 was in for "saved addresses" — the `Address` entity existed, the list was just always empty), so AC #3's empty state is what will actually render whenever this is manually checked. That's expected, not a bug.

### Status label mapping — 5 enum values, 4 AC-specified labels

`OrderStatus` (`Domain/Enums/OrderStatus.cs`) has `Pending`, `Processing`, `Shipped`, `Delivered`, `Cancelled`. AC #5 only specifies 4 French labels. Both `Pending` and `Processing` map to "En préparation" — see Task 1 for the reasoning.

### Order number — formatted from the GUID, not a new field

No dedicated order-number field exists. Rather than adding a migration for one now (a decision that belongs with Epic 4's checkout/order-creation story, which will actually decide the numbering scheme new orders get), this story formats a display-only number from the existing `Id`. Revisit if Epic 4 introduces a real sequential order number — this formatting becomes dead code to delete, not a breaking change to work around.

### IDOR check — filter by owner in the query itself, not as a separate check after loading

Copy the pattern from Story 2.4's `AccountService.GetProfileAsync`/addresses queries: `Orders.Where(o => o.UserId == userId)` is part of the *same* query that finds the order by ID, not a second `if (order.UserId != userId)` check performed after an unscoped load. Both approaches are functionally equivalent for correctness, but scoping in the query is the safer default (a future refactor can't accidentally remove just the ownership check while leaving the lookup in place). A `NotFoundException` (404) is thrown identically whether the order doesn't exist or belongs to someone else — never reveal via status code that a given ID is a *valid* order belonging to another customer.

## Project Structure Notes

Extends the existing `Application/Account/` folder from Story 2.4 (new Queries, new Models) — no new top-level folders. New Angular pages under the existing `features/account/pages/` tree. New Flutter screens under the existing `features/account/` tree. Adds two new routes to the existing protected-route guard on both platforms (extends, doesn't replace, Story 2.4's `authGuard`/`router.dart` redirect).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 2.5 acceptance criteria (Epic 2 section)
- `_bmad-output/implementation-artifacts/2-4-profil-client-consultation-et-modification.md` — established patterns this story extends (`[Authorize]`/`IUser` usage, `AccountService`, `authGuard`, protected routing on both clients)
- CLAUDE.md — "Ajouter une fonctionnalité (pattern standard)" section

## Dev Agent Record

### Context Reference

- Story created by direct inspection of `Order.cs`/`OrderItem.cs`/`OrderStatus.cs` (confirmed the schema already exists from Story 1.3, no checkout flow needed) and Story 2.4's `AccountService`/`authGuard` patterns. No web research needed.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors, `dotnet test` (Application.UnitTests) 77/77 (64 pre-existing + 13 new, including the post-review `GetOrdersQueryValidatorTests`). Caught and fixed a real EF Core translation bug during this build: `GetOrdersAsync`'s initial implementation called `FormatOrderNumber`/`MapStatusLabel` inside an `IQueryable.Select(...)` projection — neither is translatable to SQL, which throws at runtime (`InvalidOperationException`, not a compile error, so this would only have surfaced when actually hitting the endpoint). Fixed by materializing the page of `Order` entities first (`ToListAsync`), then projecting to DTOs client-side.
- Angular: `ng build` (production, exercises the two new lazy routes + prerendering) and `ng test` (via Edge as `ChromeHeadless`) both pass, 49/49 (43 pre-existing + 6 new).
- Flutter: no SDK available in this environment (same as every prior story) — code hand-written, not tool-verified.
- Re-verified after applying all code-review fixes (Task 13 above reflects the final, post-review run).

### Completion Notes List

- All 13 tasks implemented. Backend and Angular fully verified against real tooling; Flutter code written but unverified.
- Confirmed this story didn't need to wait for Epic 3/4 (catalogue/checkout) — `Order`/`OrderItem`/`Address` entities and their EF Core configurations already existed from Story 1.3, so the read-side (endpoints + UI) is fully buildable now. In this environment every account genuinely has zero orders, so the empty state (AC #3) is what actually renders on manual inspection — expected, not a bug, same situation as Story 2.4's always-empty "saved addresses" list.
- `OrderStatus`'s 5 enum values were mapped onto AC #5's 4 French labels by merging `Pending` and `Processing` into "En préparation" — a deliberate interpretation documented in Dev Notes, not an oversight.
- No dedicated order-number field exists on `Order` — formatted a display-only one from the entity's `Guid` rather than adding a migration for a sequential-number field, which is properly Epic 4's (checkout/order-creation) decision to make.
- IDOR guard on `GetOrderDetailAsync`: ownership (`o.UserId == userId`) is filtered in the same query that looks up the order by ID, and returns the identical `NotFoundException`/404 whether the order doesn't exist or belongs to another customer — verified by a dedicated test (`ShouldThrowNotFoundForAnotherUsersOrder_ProvingTheIdorGuard`), not just asserted in a comment.
- Extended (not duplicated) Story 2.4's existing protected-route guard on both platforms: Angular's `authGuard` was already generic and just needed `canActivate` added to the two new routes; Flutter's `router.dart` redirect check was broadened from an exact `/compte` match to also cover `/compte/commandes*`.
- Scope boundary respected: no address-management UI, no order cancellation/actions, no checkout — this story is read-only, matching the AC exactly.

### File List

**Backend:**
- `backend/MonEcommerce/src/Application/Account/Models/PagedResult.cs`, `OrderSummaryDto.cs`, `OrderItemDto.cs`, `OrderDetailDto.cs` (new)
- `backend/MonEcommerce/src/Application/Account/Queries/GetOrdersQuery.cs` + `GetOrdersQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Account/Queries/GetOrderDetailQuery.cs` + `GetOrderDetailQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAccountService.cs` (`GetOrdersAsync`/`GetOrderDetailAsync` added)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (implementations, status-label + order-number helpers)
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs` (`GET orders`, `GET orders/{orderId}`)
- `backend/MonEcommerce/src/Application/Account/Queries/GetOrdersQueryValidator.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceOrdersTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Queries/GetOrdersQueryValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/AuthorizationPipelineTests.cs` (updated: `StubAccountService` now implements the two new interface methods)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/account/orders.store.ts` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/orders/orders.component.ts` + `.html` (pinned `date:'UTC'`) + `.scss` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/order-detail/order-detail.component.ts` + `.html` + `.scss` + `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (two new guarded routes)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.ts` + `.html` ("Historique des commandes" link)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/account/providers/orders_provider.dart` (new; `selectedOrder` always-overwritten in `copyWith`, no longer sticky)
- `mobile/mon_ecommerce_mobile/lib/features/account/screens/orders_screen.dart`, `order_detail_screen.dart` (new; `order_detail_screen.dart`'s `initState` no longer wraps the load call in `Future.microtask`)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (two new routes, broadened protected-path check)
- `mobile/mon_ecommerce_mobile/lib/features/account/screens/profile_screen.dart` ("Historique des commandes" link)
- `mobile/mon_ecommerce_mobile/test/features/account/screens/orders_screen_test.dart` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (2.5 status)
