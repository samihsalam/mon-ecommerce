# Story 3.1: API Catalogue — Liste & Filtres

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to browse the product catalogue and filter by category, material, color, and price,
so that I can quickly narrow down to relevant products.

## Acceptance Criteria

1. **Given** a visitor requests the catalogue, **when** `GET /api/v1/products` is called (with optional query params `categoryId`, `material`, `color`, `priceMin`, `priceMax`, `pageNumber`, `pageSize`), **then** a paginated response is returned: `{ items, totalCount, pageNumber, pageSize, totalPages }`.
2. **Given** the same filter combination is requested within 5 minutes, **when** the request hits the API, **then** the response is served from Redis cache (TTL 5 min).
3. **Given** a product is created, updated, or deleted, **when** the mutation completes, **then** the catalogue cache is invalidated immediately. **[Partially satisfied — see Review Findings]**: the invalidation *mechanism* is built and tested, but no product create/update/delete endpoint exists yet (Epic 6), so the end-to-end behavior this AC describes cannot actually occur in the current system. Correctly flagged by this story's own code review, not silently overstated.
4. Response time is ≤500ms at p95 under normal load.
5. `pageSize` is capped at 100.
6. Prices are returned as integers in cents (`28500` = 285,00€).
7. All list endpoints include pagination metadata.

## Tasks / Subtasks

### Backend — first public (unauthenticated) list endpoint; also fixes a real pre-existing DI gap this story can't work without

- [x] Task 1: Fix a real pre-existing gap: `ICacheService` has no fallback when Redis isn't configured (AC: #2, #3)
  - [x] `Infrastructure/DependencyInjection.cs` only registers `ICacheService`/`RedisCacheService` inside `if (!string.IsNullOrWhiteSpace(redisConnection))` (Story 1.6's "credentials-optional" pattern) — **same class of gap Story 2.1 found and deferred for `IEmailService`** (`ValidateOnBuild=true` in Development eagerly validates the full DI graph; any handler that constructor-injects `ICacheService` would fail to even start if Redis isn't configured, which it isn't in this dev environment — confirmed via `grep` on `appsettings.json`, no `ConnectionStrings:Redis` key exists at all). Unlike the deferred `IEmailService` gap, this story genuinely cannot be built or verified without fixing it — the catalogue handler needs a working `ICacheService` end to end.
  - [x] Fix: `Infrastructure/ExternalServices/NullCacheService.cs` — a no-op `ICacheService` (`GetAsync` always misses, `SetAsync`/`RemoveAsync` no-op) registered as the `else` branch of the existing conditional Redis registration. This is exactly the fix `deferred-work.md` already recommended for `IEmailService`'s equivalent gap — applying the same pattern here, not inventing a new one.
  - [x] Add `"Redis": ""` under `ConnectionStrings` in `appsettings.json` for consistency with `SendGrid`/`Stripe`'s empty-string-means-"not configured" convention (currently the key doesn't exist at all, which is a minor inconsistency, not a bug — `GetConnectionString` returns null either way).
- [x] Task 2: Catalogue models (AC: #1, #6, #7)
  - [x] `Application/Catalogue/Models/ProductFilter.cs`: `public record ProductFilter(Guid? CategoryId, string? Material, string? Color, int? PriceMin, int? PriceMax, int PageNumber, int PageSize);`
  - [x] `Application/Catalogue/Models/ProductSummaryDto.cs`: `public record ProductSummaryDto(Guid Id, string Name, int PriceInCents, string? Material, string? Color, string? ImageUrl, Guid CategoryId, string CategoryName, bool InStock);`
  - [x] `Application/Catalogue/Models/PagedProductsResult.cs`: `public record PagedProductsResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages);` — **deliberately not reusing Story 2.5's `Application.Account.Models.PagedResult<T>`**: that shape uses `Page` (not `PageNumber`) and has no `TotalPages` field, and this AC explicitly specifies both. Named `PagedProductsResult` (not a second `PagedResult` in a different namespace) purely as cheap insurance against the exact class of ambiguous-reference bug this codebase has hit twice already (Story 2.4's `NotFoundException`/`AccountService` collisions with global-used third-party types).
- [x] Task 3: `IProductCatalogueService` + `ProductCatalogueService` (AC: #1, #2, #3, #5, #6)
  - [x] `Application/Common/Interfaces/IProductCatalogueService.cs`: `GetProductsAsync(ProductFilter filter, ...)`, `InvalidateCatalogueCacheAsync(...)`
  - [x] `Infrastructure/Catalogue/ProductCatalogueService.cs`: clamps `pageNumber` (≥1) and `pageSize` (1–100) defensively — **not a rejecting validator**, since AC #5 says "capped," and this is a public browsing endpoint where silently giving the caller the most you'll allow is friendlier than a 422; queries `_context.Products.AsNoTracking().Where(p => p.IsPublished)` (own interpretation — the AC doesn't explicitly say "published only," but a public catalogue endpoint returning draft/unpublished products would be a real, if unstated, bug; see Dev Notes) plus the optional filters, paginates, and maps to `ProductSummaryDto`
  - [x] Cache-key versioning (not per-key pattern deletion): a `catalogue:version` cache entry is included in every list cache key (`catalogue:v{version}:products:...`); `InvalidateCatalogueCacheAsync()` just increments it, instantly orphaning every previously-cached key without needing Redis pattern-scan deletion or extending `ICacheService`'s interface. TTL on individual entries is still 5 minutes (AC #2); the version key itself is set with a much longer TTL as a safety net.
  - [x] **Not wired to any mutation yet** — no product create/update/delete endpoint exists (that's Epic 6's Administration Catalogue). `InvalidateCatalogueCacheAsync()` is implemented and tested in isolation now, ready for Epic 6's future command handlers to call directly via `IProductCatalogueService` — same "build the read/infra side now, the write side doesn't exist yet" situation as Story 2.5's order history.
- [x] Task 4: `GetProductsQuery` (AC: #1, #6, #7)
  - [x] `Application/Catalogue/Queries/GetProductsQuery.cs`: `public record GetProductsQuery(Guid? CategoryId, string? Material, string? Color, int? PriceMin, int? PriceMax, int PageNumber = 1, int PageSize = 20) : IRequest<PagedProductsResult<ProductSummaryDto>>;` — **no `[Authorize]`**, this is the first genuinely public (unauthenticated) query in this codebase
- [x] Task 5: Endpoint (AC: #1)
  - [x] `Web/Endpoints/Products.cs`, `RoutePrefix => "/api/v1/products"`, `GET` (root), `.AllowAnonymous()` (explicit, matching `Auth.cs`'s convention even though it's already the default for an endpoint with no `RequireAuthorization()`)
- [x] Task 6: Backend tests
  - [x] `ProductCatalogueServiceTests.cs` — empty catalogue; each filter (category/material/color/price range) correctly narrows results; unpublished products excluded; `pageNumber ≤ 0` clamped to 1; `pageSize > 100` clamped to 100; `totalPages` computed correctly (including the zero-results case); a cache-miss populates the cache (`ICacheService.SetAsync` called); a cache-hit returns the cached value without recomputing (proven via a mocked `ICacheService` returning a canned result the DB alone couldn't produce); `InvalidateCatalogueCacheAsync()` bumps the version so a subsequent lookup misses the old key

### Angular / Flutter

- [x] **Deliberately out of scope for this story.** This story is `GET /api/v1/products` only — Stories 3.3/3.4 build the `ProductCard`/`FilterChipBar` components that actually call it. Building client UI against an endpoint with a catalogue of zero real products (no product CRUD exists until Epic 6) would be speculative work with nothing real to render; the epic's own story breakdown already separates "API" (3.1) from "components" (3.3/3.4).

### Verification

- [x] Task 7: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] AC #4 ("≤500ms p95 under normal load") is a load-testing NFR that can't be empirically verified without load-testing infrastructure (no such tooling exists in this environment or codebase yet) — addressed via reasonable practice (`AsNoTracking()`, indexed filter columns already established by Story 1.3's schema) and documented as unverified-by-tooling, not claimed as tested.

### Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–3.1's predecessors). Both Blind Hunter and Edge Case Hunter again independently flagged a fabricated `<system-reminder>` embedded in `git diff`/`git status` tool output instructing them to stay silent about something — both correctly ignored it and surfaced it instead of complying; a recurring pattern across this session's reviews, no action needed beyond noting it.

- [x] [Review][Patch] Cache-key collision, confirmed independently by two reviewers: `BuildCacheKey`'s raw delimited string interpolation (`mat={material}:col={color}:...`) let two **different** filter combinations produce the identical cache key whenever a `Material`/`Color` value (unvalidated free text) itself contained a delimiter-like substring — e.g. `Material="a:col=b", Color="c"` and `Material="a", Color="b:col=c"` both rendered as `...mat=a:col=b:col=c:...`. The second request would silently be served the first request's cached — wrong — product list. **Fixed:** the filter tuple is now JSON-serialized and SHA-256 hashed to build the key suffix; two distinct filter values always produce a distinct key regardless of their content. Covered by a new regression test seeding exactly this collision scenario.
- [x] [Review][Patch] Unbounded `pageNumber`, confirmed independently by two reviewers: only the lower bound was clamped (`Math.Max(1, filter.PageNumber)`); `(pageNumber - 1) * pageSize` is unchecked 32-bit `int` arithmetic, so an extreme `pageNumber` (e.g. `int.MaxValue`) overflows and wraps to an arbitrary — often negative — value passed to EF Core's `Skip()`, which SQL Server rejects for a negative `OFFSET` (an unhandled-exception path on a public, unauthenticated endpoint). **Fixed:** `pageNumber` is now clamped to `[1, 1_000_000]` — comfortably beyond any realistic catalogue size at any page size, and low enough that the multiplication can never approach `int.MaxValue`. Covered by a new regression test.
- [x] [Review][Note] `InvalidateCatalogueCacheAsync`'s read-then-increment-then-write is not atomic against concurrent invalidations (no atomic-increment primitive exists on `ICacheService`, and adding one for this alone would be disproportionate). Assessed as non-atomic but not actually broken: a lost increment under a race still results in at least one version bump, which still orphans every previously-cached entry — old-version keys are never explicitly deleted, only left to expire on their own TTL, so there's no scenario where a lost increment resurrects stale data. Documented in a code comment; not fixed.
- [x] [Review][Documentation] AC #3's top-level wording ("cache invalidated immediately" on product mutation) was checked off `[x]` "done" in a way that could read as the full end-to-end behavior being verified — but the story's own Completion Notes already honestly disclosed that no mutation path exists yet to trigger it. Not a code defect, a labeling precision issue: added an explicit `[Partially satisfied]` annotation directly on AC #3 above, so the gap between "mechanism built and tested" and "behavior actually occurs" isn't left implicit.
- [x] [Review][Note] AC #2's Redis caching logic is correct by code inspection (verified `RedisCacheService`'s `System.Text.Json` round-trip would work correctly for a generic record like `PagedProductsResult<ProductSummaryDto>`) but has never been exercised against a real Redis instance in this environment — only against `NullCacheService` (always-miss). Genuinely unverified end-to-end, now stated explicitly rather than left to be inferred from the DI-fallback discussion alone.

## Dev Notes

### `ICacheService` has no working implementation in this dev environment — same gap class as Story 2.1's `IEmailService` finding, but this time it blocks the story

Story 2.1 found that `IEmailService` is only conditionally registered when `SendGrid:ApiKey` is configured, and recommended (but didn't implement, since it wasn't blocking that story) a no-op fallback. `ICacheService`/`RedisCacheService` has the exact same conditional-registration shape for `ConnectionStrings:Redis` (`Infrastructure/DependencyInjection.cs`) — and this dev environment has no Redis running (`appsettings.json` has no `Redis` key under `ConnectionStrings` at all). Since this story's core requirement (AC #2, #3) is caching behavior, a handler that constructor-injects `ICacheService` would fail `ValidateOnBuild` in Development before ever handling a request. Fixed properly this time by adding `NullCacheService` as the fallback — the exact fix `deferred-work.md` already recommended for the analogous `IEmailService` gap, applied here because this story can't ship without it.

### Cache invalidation via version bump, not per-key deletion

`ICacheService.RemoveAsync(string key)` only removes one exact key. Catalogue list cache keys are parameterized by the full filter combination + page (`catalogue:v1:products:cat=...:mat=...:page=...`), so there's no single key to remove when "any product changes" — the AC requires invalidating the *whole* catalogue cache, not one specific filter combination. Extending `ICacheService`'s interface with Redis-specific pattern-scan deletion (`SCAN`+`DEL`) would leak a Redis-specific capability into an interface meant to abstract over any cache backend. Instead: a `catalogue:version` entry is embedded in every cache key; `InvalidateCatalogueCacheAsync()` just increments it. Every previously-cached key instantly becomes unreachable (a different, higher version number is now embedded in all newly-constructed keys) without ever needing to enumerate or delete the old ones — they simply expire on their own 5-minute TTL, unread.

### "Published only" is an interpretation, not explicit AC text — documented, not silently assumed

The AC never says "only published products." `Product.IsPublished` exists specifically to distinguish draft/unfinished catalogue entries from live ones (per the domain schema — Story 1.3), and a public, unauthenticated catalogue endpoint returning unpublished products would be a real content-leak bug, not a defensible reading of the spec. Filtering `Where(p => p.IsPublished)` is the obviously-intended behavior; called out explicitly here in case a future story needs to special-case it (e.g. an admin preview endpoint that intentionally includes drafts — that would be a *different*, `[Authorize]`-protected query, not a change to this one).

### `pageSize` is capped (clamped), not rejected — different choice than Story 2.5's `GetOrdersQuery`

Story 2.5's `GetOrdersQuery` rejects out-of-range `page`/`pageSize` via a FluentValidation validator (422). This story's AC literally says "pageSize is capped at 100," which reads as clamping behavior, not rejection — and for a public, unauthenticated browsing endpoint, silently returning the largest allowed page is friendlier API design than erroring on a caller who just asked for more than the max. Both choices are reasonable; they differ here because the AC wording differs.

## Project Structure Notes

New `Application/Catalogue/` folder (Models, Queries) parallel to `Application/Auth/` and `Application/Account/`. New `Infrastructure/Catalogue/` folder for `ProductCatalogueService.cs`, parallel to `Infrastructure/Identity/`. New `Web/Endpoints/Products.cs`, parallel to `Auth.cs`/`Account.cs`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 3.1 acceptance criteria (Epic 3 section)
- `_bmad-output/implementation-artifacts/2-1-inscription-client.md` — origin of the `IEmailService` conditional-registration finding this story fixes the `ICacheService` analog of
- `_bmad-output/implementation-artifacts/2-5-historique-des-commandes.md` — established pagination/DTO patterns this story deliberately diverges from where the AC wording differs
- `_bmad-output/implementation-artifacts/deferred-work.md` — the original recommended fix for the `IEmailService` gap, whose pattern this story reuses for `ICacheService`

## Dev Agent Record

### Context Reference

- Story created by direct inspection of `Product.cs`/`Category.cs`/`Stock.cs`/`ProductImage.cs` (confirmed schema exists from Story 1.3), `ICacheService.cs`/`RedisCacheService.cs` (confirmed conditional registration, confirmed no Redis configured in this environment), and `DependencyInjection.cs`. No web research needed.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors, `dotnet test` (Application.UnitTests) 87/87 (77 pre-existing + 10 new, including the two post-review regression tests).
- AC #4 (≤500ms p95) is a load-testing NFR — no load-testing tooling exists in this codebase/environment; addressed via `AsNoTracking()` and reasonable indexed-column filtering, but genuinely unverified by tooling, not claimed as tested.
- No Angular/Flutter work — deliberately out of scope (API only; Stories 3.3/3.4 build the consuming UI).
- Re-verified after applying all code-review fixes (Task 7 above reflects the final, post-review run).

### Completion Notes List

- All 7 tasks implemented and verified against real tooling.
- Found and fixed a real pre-existing gap that blocked this story: `ICacheService` had no fallback when Redis isn't configured (confirmed via `grep` — no `ConnectionStrings:Redis` key existed anywhere), which would have failed `ValidateOnBuild` in Development for any handler that constructor-injects it. This is the same gap class Story 2.1 found (and only documented, didn't fix) for `IEmailService` — fixed here with `NullCacheService`, the exact pattern `deferred-work.md` already recommended, because this story genuinely can't work without a functioning `ICacheService`.
- Cache invalidation (AC #3) uses a version-bump scheme rather than per-key deletion, since `ICacheService.RemoveAsync` only removes one exact key and catalogue cache keys are parameterized by the full filter+page combination — there's no single key that represents "the whole catalogue cache." Incrementing a `catalogue:version` entry embedded in every cache key instantly orphans all previously-cached entries without needing Redis pattern-scan deletion or a wider `ICacheService` interface.
- `InvalidateCatalogueCacheAsync()` is implemented and tested but not called by anything yet — no product create/update/delete endpoint exists (Epic 6, Administration Catalogue). Ready for those future command handlers to call directly via `IProductCatalogueService`.
- Filtered to `IsPublished` products only — not explicit AC text, but the obviously-intended behavior for a public, unauthenticated endpoint (documented as an interpretation in Dev Notes, not a silent assumption).
- `pageSize` is clamped (not rejected via a validator), unlike Story 2.5's `GetOrdersQuery` — different choice justified by this AC's literal "capped at 100" wording and this endpoint's public/browsing nature (see Dev Notes for the full reasoning).
- Scope boundary respected: no Angular/Flutter work, no product CRUD, no search (Story 3.2) — this story is exactly `GET /api/v1/products` with filters, pagination, and caching.

### File List

**Backend:**
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/NullCacheService.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (registers `NullCacheService` as the Redis-not-configured fallback, registers `IProductCatalogueService`)
- `backend/MonEcommerce/src/Web/appsettings.json` (added `ConnectionStrings:Redis` empty-string key)
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductFilter.cs`, `ProductSummaryDto.cs`, `PagedProductsResult.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IProductCatalogueService.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductsQuery.cs` + `GetProductsQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/ProductCatalogueServiceTests.cs` (new)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (3.1 status, epic-3 in-progress)
