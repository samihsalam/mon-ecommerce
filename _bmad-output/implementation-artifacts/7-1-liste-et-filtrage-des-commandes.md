# Story 7.1: Liste & Filtrage des Commandes

Status: done

## Story

As an administrator,
I want to view all orders with filtering by status, date, and customer,
so that I have a complete operational overview of the shop.

## Acceptance Criteria

1. Given an admin accesses the orders list, when `GET /api/v1/admin/orders` is called, then a paginated list is returned with columns: order number, customer name, date, total amount (cents), status.
2. Given filter parameters are provided, when `GET /api/v1/admin/orders?status=...&dateFrom=2026-04-01&dateTo=2026-04-12&search=Salma` is called, then only matching orders are returned. **`status`'s accepted value differs from the AC's own French example** — see Dev Notes.
3. Results are sorted by date descending by default.
4. Response time is ≤ 500ms at p95. **Design property, not test-asserted** — see Dev Notes.
5. Pagination metadata is included (`totalCount`, `pageNumber`, `pageSize`, `totalPages`).
6. Only users with the `Admin` role can access this endpoint.

## Tasks / Subtasks

- [x] Task 1: Extract `Application/Common/OrderStatusLabelFormatter.cs` from `AccountService.MapStatusLabel` (needed in a second place — this story's admin list — same refactor precedent as `OrderNumberFormatter`/`ReturnStatusLabelFormatter`, Stories 5.3/5.4). `AccountService.MapStatusLabel` becomes a thin delegate, same shape as its existing `MapReturnStatusLabel` → `ReturnStatusLabelFormatter.Format`.
- [x] Task 2: `Application/Orders/Models/{AdminOrderSummaryDto,AdminOrderFilter,PagedOrdersResult}.cs`. `PagedOrdersResult<T>` mirrors `Catalogue.Models.PagedProductsResult<T>`'s shape (`Items, TotalCount, PageNumber, PageSize, TotalPages`) — AC #5's exact field names — deliberately **not** reused directly (misleadingly named for products) and deliberately **not** matching `Account.Models.PagedResult<T>`'s different, pre-existing shape (`Page`, no `TotalPages`). See Dev Notes for why neither existing type was extended.
- [x] Task 3: `Application/Common/Interfaces/IAdminOrderService.cs` + `Infrastructure/Orders/AdminOrderService.cs` (same "Infrastructure service does the real EF Core query, Application handler just delegates" split as `IAccountService`/`IProductCatalogueService`) — needed because filtering/searching by customer name requires joining `Orders` with `AspNetUsers` (`ApplicationDbContext.Users`, only reachable from Infrastructure; `IApplicationDbContext` doesn't expose it, by design — Identity stays out of the Application layer). Batches customer-name resolution for the current page in one query (`Dictionary<userId, name>`), not per-row, to avoid N+1 (AC #4).
- [x] Task 4: `Application/Orders/Queries/GetAdminOrdersQuery.cs` + Handler + Validator (AC #1, #2, #3, #5, #6). `[Authorize(Roles = Roles.Administrator)]`.
- [x] Task 5: `Web/Endpoints/AdminOrders.cs` — `GET ""` (root of `/api/v1/admin/orders`) mapped to a method named `GetAdminOrders`, not `GetOrders` — `Web/Endpoints/Account.cs` already has a `GetOrders` (the customer-facing equivalent); a same-named handler in a different `IEndpointGroup` would collide (ASP.NET Core Minimal API endpoint names, inferred from the method name for method-group handlers, must be globally unique — the exact class of bug just fixed live while running the app, see the `fix(backend)` commit).
- [x] Task 6: Unit tests — `AdminOrderServiceTests` (status filter, date range filter, customer-name search, pagination metadata, default date-descending sort, no-N+1 name resolution), `GetAdminOrdersQueryValidatorTests`.

## Dev Notes

### AC #2's `status=Expédiée` example doesn't match this codebase's actual `OrderStatus` representation

`OrderStatus` has been an English-named C# enum (`Pending`, `Processing`, `Shipped`, `Delivered`, `Cancelled`), serialized/bound as such, consistently since Story 1.3 — every existing endpoint that accepts or returns it (`UpdateOrderStatusCommand`, Story 5.2's `AdminOrders.UpdateOrderStatus`, the customer-facing `OrderSummaryDto.Status` which is a formatted *display* string, not the wire enum) already works this way. Introducing a French-string-keyed enum representation (`JsonStringEnumConverter` with custom names, or a bespoke parser) would be a cross-cutting change touching every existing OrderStatus/ReturnStatus/ReturnReason call site, several epics deep, entirely out of proportion for one story's illustrative query-string example. `?status=Shipped` (the actual enum member name, ASP.NET Core's default query-string-to-enum binding) is what this story implements; flagged rather than silently reinterpreted.

### AC #4 (≤500ms p95) is a design property here, not something a unit test asserts

EF Core's InMemory provider (this codebase's only test double for these queries) has no meaningful relationship to real SQL Server latency under a production data volume — a passing "under Xms" unit test wouldn't actually demonstrate the AC, only that InMemory is fast, which was never in question. What IS implemented and verifiable: no N+1 queries (customer names for a page are resolved in one batched `Dictionary` lookup, not one query per order), and filtering/sorting happen entirely in the SQL query (`Where`/`OrderBy` before `Skip`/`Take`), not after materializing the whole table. Real p95 depends on production indexing/infrastructure this story doesn't provision.

### Why a new `PagedOrdersResult<T>` instead of reusing or unifying the two existing paged-result shapes

This codebase already has two different "paged list" record shapes: `Account.Models.PagedResult<T>` (`Items, TotalCount, Page, PageSize` — no `TotalPages`, used by the customer's own order history) and `Catalogue.Models.PagedProductsResult<T>` (`Items, TotalCount, PageNumber, PageSize, TotalPages` — Story 3.x). AC #5 names fields that match the second shape, not the first. Renaming/unifying either pre-existing type to eliminate the duplication is a real cleanup opportunity but touches already-shipped, tested code outside this story's scope; flagged here as a known inconsistency, not fixed unasked. A third, admin-orders-scoped type was added instead of extending either.

## Project Structure Notes

New: `Application/Common/OrderStatusLabelFormatter.cs`, `Application/Orders/Models/{AdminOrderSummaryDto,AdminOrderFilter,PagedOrdersResult}.cs`, `Application/Common/Interfaces/IAdminOrderService.cs`, `Infrastructure/Orders/AdminOrderService.cs`, `Application/Orders/Queries/GetAdminOrdersQuery.cs` (+ Handler + Validator), unit tests under `tests/Application.UnitTests/Orders/`. Modified: `Infrastructure/Identity/AccountService.cs` (delegates to the extracted formatter), `Infrastructure/DependencyInjection.cs` (`IAdminOrderService` registration), `Web/Endpoints/AdminOrders.cs`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 7.1 acceptance criteria (Epic 7 section, line ~1140).
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` — `MapStatusLabel`/`FormatOrderNumber`, the source this story extracts `OrderStatusLabelFormatter` from and reuses `OrderNumberFormatter` from.
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` — the "Infrastructure service does the EF Core query, Application handler delegates" pattern this story's `AdminOrderService` follows, and `PagedProductsResult<T>`'s shape this story's `PagedOrdersResult<T>` mirrors.
- The live `fix(backend)` commit (just before this story) — the endpoint-method-name-uniqueness lesson `GetAdminOrders` (not `GetOrders`) directly applies here.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Confirmed `Order` has no EF navigation to `ApplicationUser` (Identity's tables are deliberately not modeled as a foreign relationship from `Order`) — customer-name search/resolution goes through `UserManager<ApplicationUser>.Users` (two bounded queries: matching ids, then a page's names), not a LINQ join.
- Unit-tested against a real `UserStore<ApplicationUser>` backed by the same InMemory `ApplicationDbContext`, not a `Mock<UserManager<...>>` — `AccountServiceOrdersTests`' existing mock only ever exercises `FindByIdAsync`, but `AdminOrderService` needs `.Users` to be a genuinely queryable, seeded data source for its search/name-resolution tests.

### Completion Notes List

- `GET /api/v1/admin/orders` implemented with status/date-range/customer-name-search filtering, default date-descending sort, and `{ totalCount, pageNumber, pageSize, totalPages }` pagination metadata (AC #1, #2, #3, #5).
- `OrderStatusLabelFormatter` extracted from `AccountService.MapStatusLabel` (now a one-line delegate, same shape as its existing `MapReturnStatusLabel`) — reused by the new admin list so both the customer-facing and admin-facing order status strings stay a single source of truth.
- `IAdminOrderService`/`AdminOrderService` (Infrastructure) batch-resolve customer names for the current page in one query, and resolve search matches in one query before filtering orders by the resulting id list — two bounded round trips regardless of result size, not one query per order (AC #4's N+1-avoidance design property).
- AC #2's `status=Expédiée` example doesn't match this codebase's actual `OrderStatus` wire representation (English enum member names, unchanged since Story 1.3) — implemented as `?status=Shipped` etc.; flagged explicitly in the AC list and Dev Notes rather than silently reinterpreted or triggering a much larger, out-of-scope enum-serialization overhaul.
- AC #4 (≤500ms p95) is documented as a design property (no N+1, filtering done in SQL before pagination) rather than asserted by a specific timing test — EF Core's InMemory provider has no meaningful relationship to real SQL Server latency.
- New `PagedOrdersResult<T>` mirrors `Catalogue.Models.PagedProductsResult<T>`'s field names (matching AC #5 exactly) rather than reusing that misleadingly-"Products"-named type or the differently-shaped `Account.Models.PagedResult<T>` — the pre-existing inconsistency between those two is flagged, not fixed, in Dev Notes.
- `AdminOrders.GetAdminOrders` (not `GetOrders`) — avoids colliding with `Account.GetOrders`'s endpoint name, applying the lesson from the `fix(backend)` commit that immediately preceded this story.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 329/329 Application.UnitTests passing, including 12 new tests (`AdminOrderServiceTests`, `GetAdminOrdersQueryValidatorTests`). No migration needed — no schema changes. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Application/Common/OrderStatusLabelFormatter.cs`
- `backend/MonEcommerce/src/Application/Orders/Models/{AdminOrderSummaryDto,AdminOrderFilter,PagedOrdersResult}.cs`
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAdminOrderService.cs`
- `backend/MonEcommerce/src/Infrastructure/Orders/AdminOrderService.cs`
- `backend/MonEcommerce/src/Application/Orders/Queries/GetAdminOrdersQuery.cs` (+ Handler, Validator)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/Services/AdminOrderServiceTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/Queries/GetAdminOrdersQueryValidatorTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (`MapStatusLabel` delegates to the extracted formatter)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (`IAdminOrderService` registration)
- `backend/MonEcommerce/src/Web/Endpoints/AdminOrders.cs` (`GET` root route)
