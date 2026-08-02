# Story 7.5: Analytics Produits & Stocks Critiques

Status: done

## Story

As an administrator,
I want to see the most viewed and best-selling products and those with critically low stock,
so that I can optimize the catalogue and anticipate restocking needs.

## Acceptance Criteria

1. Given an admin requests product analytics, when `GET /api/v1/admin/analytics/top-products` is called, then top 10 most viewed and top 10 best-selling products for the last 7 days are returned.
2. Given an admin requests low stock alerts, when `GET /api/v1/admin/analytics/low-stock` is called, then all products with `stock ≤ alertThreshold` are returned with: name, current stock, threshold, and a direct link to the product edit page.
3. Top-products data is cached in Redis with TTL 1 hour.
4. Low-stock data is always read directly from PostgreSQL (never cached).
5. The low-stock list is also surfaced as a widget on the main dashboard. **Satisfied by AC #2's endpoint itself, no additional backend work** — see Dev Notes.
6. Product view tracking increments a counter on each `GET /api/v1/products/{id}` call.

## Tasks / Subtasks

- [x] Task 1: `Domain/Entities/ProductDailyViewCount.cs` (`BaseEntity` — no audit fields needed, this is a pure counter, not an admin-attributable action): `ProductId`, `Date` (`DateOnly`), `ViewCount`. Unique index on `(ProductId, Date)` — one row per product per day, incremented in place, not one row per view (AC #1's "last 7 days" only ever needs a `SUM(ViewCount) WHERE Date >= ...`, and per-day rows keep that query's row count bounded regardless of traffic volume). `Infrastructure/Data/Configurations/ProductDailyViewCountConfiguration.cs` + migration. `IApplicationDbContext`/`ApplicationDbContext` gain `DbSet<ProductDailyViewCount> ProductDailyViewCounts`.
- [x] Task 2: `GetProductByIdQueryHandler` (AC #6) gains a second constructor dependency (`IApplicationDbContext`, plus `TimeProvider` for a testable "today") and increments the current day's `ProductDailyViewCount` row after `IProductCatalogueService.GetProductByIdAsync` succeeds — deliberately **not** inside `ProductCatalogueService`'s own cached read path, since that method is skipped entirely on a Redis cache hit (5-minute TTL, Story 3.x), which would silently under-count views for every request served from cache. Read-then-write (find-or-create the day's row), not a single atomic upsert — acceptable for a soft, imprecision-tolerant analytics counter (a rare race under heavy concurrent traffic to the same product+day could at most drop one increment), not attempted for financial or stock data anywhere in this codebase. No increment on a 404 (unpublished/missing product) — only a successful view counts.
- [x] Task 3: `Application/Common/Interfaces/IAnalyticsService.cs` + `Infrastructure/Catalogue/AnalyticsService.cs` (AC #1, #2, #3, #4). `GetTopProductsAsync`: most-viewed (sum `ProductDailyViewCount.ViewCount` over the last 7 days, grouped by product, top 10) and best-selling (sum `OrderItem.Quantity` for orders placed in the last 7 days, excluding `Cancelled` — same "cancelled orders don't count as real business" convention as Story 7.4's dashboard — grouped by product, top 10), both computed together and cached as one Redis entry (`analytics:top-products`, 1-hour TTL). `GetLowStockProductsAsync`: `Stock.Quantity <= Stock.AlertThreshold`, joined to `Product` (excluding soft-deleted), **no caching anywhere in this method** — AC #4's own explicit requirement.
- [x] Task 4: `Application/Catalogue/Queries/{GetTopProductsQuery,GetLowStockProductsQuery}.cs` + Handlers (AC #1, #2). `[Authorize(Roles = Roles.Administrator)]`. `Application/Catalogue/Models/{TopProductsDto,ProductAnalyticsSummaryDto,LowStockProductDto}.cs`. `LowStockProductDto.EditUrl` is built from `Frontend:BaseUrl` config using a placeholder route shape (`/admin/produits/{id}`) — no admin frontend exists anywhere in this codebase yet, so the actual route this should point to is unconfirmed; flagged, not silently guessed at as if it were verified.
- [x] Task 5: `Web/Endpoints/AdminAnalytics.cs` (new endpoint group) — `GET /api/v1/admin/analytics/top-products`, `GET /api/v1/admin/analytics/low-stock`.
- [x] Task 6: Unit tests — new `GetProductByIdQueryHandlerTests` (first view creates a row, a second same-day view increments it, no increment on `NotFoundException`, a new day creates a separate row), new `AnalyticsServiceTests` (7-day window boundaries for both lists, `Cancelled` orders excluded from best-selling, low-stock threshold comparison, cache read/write verified for top-products, no cache interaction verified for low-stock). No dedicated tests for `GetTopProductsQueryHandler`/`GetLowStockProductsQueryHandler` or a validator — both are one-line delegators to `IAnalyticsService` with no parameters to validate, already exercised end-to-end by `AnalyticsServiceTests`, same precedent as Story 7.1/7.3's other thin query handlers.

## Dev Notes

### AC #5 needs no new backend work

"The low-stock list is also surfaced as a widget on the main dashboard" describes a frontend composition detail (which endpoint a dashboard page's widget calls), not a distinct backend capability — the exact same `GET /api/v1/admin/analytics/low-stock` data this story already builds for AC #2 is what such a widget would consume. Extending Story 7.4's `GetDashboardMetricsQuery`/`DashboardMetricsDto` to embed the low-stock list inline was considered and rejected: it would force every dashboard poll to also pay for a low-stock query it may not need, and duplicates a resource this story already exposes at its own URL for no benefit.

### View counts are tracked outside `ProductCatalogueService`'s cached path, not inside it

`ProductCatalogueService.GetProductByIdAsync` (Story 3.x) caches its result in Redis for 5 minutes — a cache hit never touches the database or re-executes any code inside that method. If the view-increment lived inside it, every cache-hit request (likely the majority of traffic for a popular product) would silently not count as a view, defeating AC #6 for exactly the products AC #1's "most viewed" ranking cares about most. Incrementing from the query handler instead means the MediatR pipeline (which always runs, cache hit or not) is what drives the counter.

### Low-stock's `EditUrl` route is a placeholder, not a confirmed contract

No admin frontend exists anywhere in this codebase (Epic 6/7's now-repeated finding) — there is no real `/admin/produits/{id}` route to link to yet. AC #2 asks for "a direct link to the product edit page," so *some* URL had to be built; documented explicitly as unconfirmed so a future admin-frontend story doesn't mistake it for an already-agreed route contract.

## Project Structure Notes

New: `Domain/Entities/ProductDailyViewCount.cs`, `Infrastructure/Data/Configurations/ProductDailyViewCountConfiguration.cs` (+ migration), `Application/Common/Interfaces/IAnalyticsService.cs`, `Infrastructure/Catalogue/AnalyticsService.cs`, `Application/Catalogue/Queries/{GetTopProductsQuery,GetLowStockProductsQuery}.cs` (+ Handlers), `Application/Catalogue/Models/{TopProductsDto,ProductAnalyticsSummaryDto,LowStockProductDto}.cs`, `Web/Endpoints/AdminAnalytics.cs`, unit tests under `tests/Application.UnitTests/Catalogue/`. Modified: `Application/Common/Interfaces/IApplicationDbContext.cs`, `Infrastructure/Data/ApplicationDbContext.cs`, `Application/Catalogue/Queries/GetProductByIdQueryHandler.cs`, `Infrastructure/DependencyInjection.cs` (`IAnalyticsService` registration).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 7.5 acceptance criteria (Epic 7 section, line ~1230) — the last story in Epic 7.
- `_bmad-output/implementation-artifacts/7-4-dashboard-kpis-temps-reel.md` — the "cancelled orders excluded from real business metrics" convention this story's best-selling calculation reuses.
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` — the cached `GetProductByIdAsync` this story's view-tracking placement decision is about, and the `ICacheService`/versioned-cache-key convention `AnalyticsService`'s top-products caching follows.
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductByIdQueryHandler.cs` — where AC #6's increment is wired in.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Confirmed `EF Core`'s `GroupBy → aggregate → OrderByDescending → Take → Join` chain (used for both `MostViewed`/`BestSelling` in `AnalyticsService.GetTopProductsAsync`) translates to a single SQL query without needing to materialize the table first — verified via the build/test cycle rather than assumed.
- `NullCacheService` (Infrastructure/ExternalServices) already existed and is public, but `AnalyticsServiceTests` defines its own minimal always-miss stub rather than reference it directly, to keep the test file self-contained the same way other test files in this codebase already do.

### Completion Notes List

- `GET /api/v1/admin/analytics/top-products` (AC #1, #3) — top 10 most-viewed and top 10 best-selling products over the last 7 days, cached as one Redis entry (`analytics:top-products`, 1-hour TTL).
- `GET /api/v1/admin/analytics/low-stock` (AC #2, #4) — products at or below their stock alert threshold, read directly from the database on every call, no caching anywhere in that code path.
- AC #6 (view tracking) implemented by having `GetProductByIdQueryHandler` increment a `ProductDailyViewCount` row itself, deliberately outside `ProductCatalogueService`'s cached read path — a cache hit would otherwise silently skip the increment for exactly the popular, frequently-viewed products AC #1's ranking cares about most.
- `Cancelled` orders excluded from the best-selling calculation, reusing Story 7.4's "not real business" convention.
- AC #5 (low-stock list surfaced as a dashboard widget) required no additional backend work — the same `/analytics/low-stock` endpoint already built for AC #2 is what such a widget would call; extending Story 7.4's dashboard DTO to embed it inline was considered and rejected (would force every dashboard poll to also pay for a query it may not need).
- `LowStockProductDto.EditUrl` is explicitly flagged as a placeholder route (`/admin/produits/{id}`) — no admin frontend exists anywhere in this codebase to confirm the real route against.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 379/379 Application.UnitTests passing, including 12 new tests (`GetProductByIdQueryHandlerTests`, `AnalyticsServiceTests`). Migration `AddProductDailyViewCount` generated. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test/migrate on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).
- This is the last story in Epic 7 — all five stories (7.1–7.5) are now done.

### File List

**New:**
- `backend/MonEcommerce/src/Domain/Entities/ProductDailyViewCount.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/ProductDailyViewCountConfiguration.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260802102828_AddProductDailyViewCount.cs` (+ `.Designer.cs`, snapshot update)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IAnalyticsService.cs`
- `backend/MonEcommerce/src/Infrastructure/Catalogue/AnalyticsService.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Queries/{GetTopProductsQuery,GetTopProductsQueryHandler,GetLowStockProductsQuery,GetLowStockProductsQueryHandler}.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Models/{TopProductsDto,ProductAnalyticsSummaryDto,LowStockProductDto}.cs`
- `backend/MonEcommerce/src/Web/Endpoints/AdminAnalytics.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Queries/GetProductByIdQueryHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/AnalyticsServiceTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductByIdQueryHandler.cs` (view-count increment)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (`ProductDailyViewCounts` DbSet)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (`ProductDailyViewCounts` DbSet)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (`IAnalyticsService` registration)
