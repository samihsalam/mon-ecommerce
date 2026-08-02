# Story 7.4: Dashboard KPIs Temps Réel

Status: done

## Story

As an administrator,
I want to see today's key metrics (revenue, orders count, average order value) at a glance,
so that I can monitor the shop's daily health efficiently.

## Acceptance Criteria

1. Given an admin opens the dashboard, when `GET /api/v1/admin/dashboard` is called, then the following metrics are returned: `revenueToday` (cents), `ordersToday`, `averageOrderValue` (cents), `revenueThisMonth`.
2. Given the dashboard is open, when 30 seconds elapse (frontend polling), then the metrics are refreshed automatically without full page reload. **Not implementable by this story** — frontend polling, no admin UI exists anywhere in this codebase yet (same precedent as Stories 6.2–7.3's admin-UI-dependent AC bullets).
3. Given today's revenue vs yesterday's revenue, when the dashboard renders, then a trend indicator is shown (↑ green / ↓ red) with the percentage difference. **Partially implementable** — the backend computes and returns the underlying data (`revenueYesterdayInCents`, `revenueTrendPercentage`); the ↑/↓ colored rendering itself is frontend, out of scope.
4. Metrics are displayed as visual cards with clear labels in French. **Not implementable by this story** — frontend rendering, no admin UI exists.
5. Amounts are formatted as currency (285,00 €) on the frontend, stored as cents in the API. **The API-side half is already how every money field in this codebase works** — verified, not built; the frontend formatting half is out of scope.
6. The dashboard loads in ≤ 1 second. **Design property, not test-asserted** — same reasoning as Stories 7.1/7.3's response-time ACs.

## Tasks / Subtasks

- [x] Task 1: `Application/Common/Models/DashboardMetricsDto.cs` (AC #1, #3) — `RevenueTodayInCents`, `OrdersToday`, `AverageOrderValueInCents`, `RevenueThisMonthInCents`, `RevenueYesterdayInCents`, `RevenueTrendPercentage` (`double?`, `null` when yesterday's revenue was zero — no meaningful percentage change to compute against a zero baseline). Field names use this codebase's own `...InCents` suffix convention (`TotalInCents`, `PriceInCents`, etc.) rather than the AC's literal bare `revenueToday`/`revenueThisMonth` — same unit (cents), consistent naming, not a functional deviation.
- [x] Task 2: `Application/Dashboard/Queries/GetDashboardMetricsQuery.cs` + Handler (AC #1, #3, #6). `[Authorize(Roles = Roles.Administrator)]`. Implemented directly against `IApplicationDbContext` in the Application layer — no separate Infrastructure service this time (unlike Story 7.1/7.3's `AdminOrderService`/`AdminReturnService`), since nothing here needs `ApplicationUser`/`UserManager` data, the reason those two needed an Infrastructure-layer indirection in the first place. All four metrics computed via SQL-level `SumAsync`/`CountAsync` (never materializing the `Orders` table into memory) — AC #6's performance design property. `Cancelled` orders excluded from every metric (a cancelled order generated no real revenue or order count) — a business-logic judgment call the AC doesn't spell out; documented, not silently assumed. "Today"/"yesterday"/"this month" computed from UTC calendar boundaries via the existing `TimeProvider` (testable, same convention as Stories 4.6/5.1).
- [x] Task 3: `Web/Endpoints/AdminDashboard.cs` (new endpoint group — the dashboard is its own resource, not nested under products/orders/returns/categories) — `GET /api/v1/admin/dashboard`.
- [x] Task 4: Unit tests — `GetDashboardMetricsQueryHandlerTests` (revenue/order-count/average scoped correctly to today vs. this month vs. yesterday, cancelled orders excluded from every metric, average-order-value division-by-zero guarded when there are no orders today, trend percentage computed correctly and `null`'d when yesterday's revenue was zero, UTC calendar-boundary correctness via a fixed `TimeProvider`).

## Dev Notes

### "Today vs. yesterday" compares today-so-far against the *full* previous day, not the same time of day

AC #3 says "today's revenue vs yesterday's revenue" without specifying whether "yesterday" means the full 24-hour day or only the portion of it that had elapsed by the current time of day (the more analytically correct comparison for a live-updating trend indicator, since a 9am snapshot compared against a full previous day will structurally look like a decline every single morning). Implemented as the simpler, literal reading — full yesterday vs. today-so-far — since the AC doesn't ask for the time-of-day-adjusted version and building it would be solving a problem not stated. Flagged as a real analytical limitation for whoever eventually builds the frontend trend indicator, not silently glossed over.

### Cancelled orders are excluded from every metric

Not stated explicitly by any AC bullet, but the only defensible reading of "revenue" and "orders count" as *business health* metrics (this story's own framing: "monitor the shop's daily health") — a cancelled order represents zero actual revenue and arguably shouldn't inflate an "orders today" count an admin is using to gauge how business is going. `OrderStatus.Cancelled` (Story 7.2's transition state machine) is the only status excluded; `Pending`/`Processing`/`Shipped`/`Delivered` all count (an order counts toward revenue from the moment it's placed, not only once delivered — consistent with how Stripe payment capture already happens at checkout, Story 4.5/4.6, well before any status transition).

### No separate Infrastructure service this time

Story 7.1's `AdminOrderService` and Story 7.3's `AdminReturnService` both needed an Infrastructure-layer indirection specifically because they had to resolve `ApplicationUser` data via `UserManager`, which `IApplicationDbContext` deliberately doesn't expose to the Application layer. This story's metrics are pure `Order` aggregates — no Identity data involved — so `GetDashboardMetricsQueryHandler` queries `IApplicationDbContext` directly, matching how most other Application-layer handlers in this codebase already work when no such cross-layer need exists.

## Project Structure Notes

New: `Application/Common/Models/DashboardMetricsDto.cs`, `Application/Dashboard/Queries/GetDashboardMetricsQuery.cs` (+ Handler), `Web/Endpoints/AdminDashboard.cs`, unit tests under `tests/Application.UnitTests/Dashboard/`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 7.4 acceptance criteria (Epic 7 section, line ~1209).
- `_bmad-output/implementation-artifacts/6-2-gestion-des-images-produit.md`, `6-3-import-csv-en-masse.md`, `7-1-liste-et-filtrage-des-commandes.md` — established precedent for flagging admin-UI-dependent AC bullets as out of a backend story's scope, and for treating a response-time AC as a design property rather than a specific timing assertion.
- `_bmad-output/implementation-artifacts/7-2-mise-a-jour-statut-et-numero-de-suivi.md` — the `OrderStatus` transition state machine this story's `Cancelled`-exclusion logic reads against.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Confirmed `TimeProvider` is already registered as a singleton (`builder.Services.AddSingleton(TimeProvider.System)`, Story 4.6-era) — no new DI wiring needed for the handler's testable UTC "now."
- Deliberately skipped the `IAdminOrderService`/`IAdminReturnService`-style Infrastructure-layer indirection Stories 7.1/7.3 both needed — this query has no reason to leave `IApplicationDbContext`, since nothing here touches `ApplicationUser`/`UserManager`.

### Completion Notes List

- `GET /api/v1/admin/dashboard` returns `revenueTodayInCents`, `ordersToday`, `averageOrderValueInCents`, `revenueThisMonthInCents`, plus `revenueYesterdayInCents`/`revenueTrendPercentage` for AC #3's trend indicator (the backend computes the underlying numbers; the actual ↑/↓ colored rendering is frontend, out of scope, no admin UI exists yet).
- All four/six metrics computed via SQL-level `SumAsync`/`CountAsync` against `IApplicationDbContext.Orders`, never materializing the table — AC #6's ≤1s design property (not asserted by a specific timing test, same precedent as Stories 7.1/7.3).
- `OrderStatus.Cancelled` orders are excluded from every metric — a business-logic judgment call not spelled out by any AC bullet, documented explicitly in Dev Notes rather than silently assumed.
- Trend percentage compares today-so-far against the *full* previous day (not time-of-day-adjusted) — the literal reading of AC #3's wording; flagged as a real analytical limitation for whoever eventually builds the frontend trend indicator.
- AC #2 (30s frontend polling), #4 (visual cards, French labels), and the frontend half of #5 (currency formatting) are explicitly out of scope — no admin UI exists anywhere in this codebase yet, same precedent as every prior Epic 6/7 admin-UI-dependent AC.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 367/367 Application.UnitTests passing, including 7 new `GetDashboardMetricsQueryHandlerTests` (today/yesterday/this-month boundary correctness, cancelled-order exclusion, division-by-zero guard, positive/negative/null trend percentage, all via a fixed `TimeProvider` so the test suite's actual run date never matters). No migration needed — no schema changes. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Application/Common/Models/DashboardMetricsDto.cs`
- `backend/MonEcommerce/src/Application/Dashboard/Queries/GetDashboardMetricsQuery.cs` (+ Handler)
- `backend/MonEcommerce/src/Web/Endpoints/AdminDashboard.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Dashboard/Queries/GetDashboardMetricsQueryHandlerTests.cs`
