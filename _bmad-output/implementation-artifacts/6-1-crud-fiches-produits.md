# Story 6.1: CRUD Fiches Produits

Status: done

## Story

As an administrator,
I want to create, edit, and delete product listings with all their details,
so that the catalogue is always accurate and up to date.

## Acceptance Criteria

1. Given valid product data (name, description, price in cents, category, material, initial stock), when `POST /api/v1/admin/products` is called, then the product is created with status "Dépublié" (not visible publicly) and its ID is returned.
2. Given an existing product, when `PUT /api/v1/admin/products/{id}` is called with updated fields, then the product is updated, the Redis catalogue cache is invalidated, and the updated product is returned.
3. Given an existing product, when `DELETE /api/v1/admin/products/{id}` is called, then the product is soft-deleted (hidden from catalogue, not physically removed) and associated data is preserved.
4. Only users with the `Admin` role can access these endpoints.
5. Price is validated as a positive integer (cents), never negative or zero.
6. All mutations are logged with admin user ID and timestamp.

## Tasks / Subtasks

- [x] Task 1: Add `Product.IsDeleted` (bool, default false) — a dedicated soft-delete flag, deliberately not reusing `IsPublished`. Story 6.5 (Catégories & Publication) owns `IsPublished` exclusively via its own `PATCH /admin/products/{id}/publish` endpoint; conflating "deleted" with "unpublished" here would let a future 6.5 publish-toggle accidentally resurrect a deleted product. `Infrastructure/Data/Configurations/ProductConfiguration.cs` needs no change (plain bool, defaults false). New migration.
- [x] Task 2: Update every `ProductCatalogueService` read path that filters `p.IsPublished` to also filter `!p.IsDeleted` (`GetProductsAsync`, `GetProductByIdAsync`, `GetSearchSuggestionsAsync`, `GetSimilarProductsAsync`, `GetSitemapEntriesAsync`) — a soft-deleted product must never appear in any public catalogue read, same as an unpublished one.
- [x] Task 3: `Application/Catalogue/Models/AdminProductDto.cs` — the admin-facing product shape (includes `IsPublished`/`StockQuantity`, unlike the public `ProductDetailDto` which only ever returns published products).
- [x] Task 4: `Application/Catalogue/Commands/CreateProductCommand.cs` + Handler + Validator (AC #1, #4, #5, #6). `[Authorize(Roles = Roles.Administrator)]`. Validates `CategoryId` references an existing category (`NotFoundException`, handler-side — no DB-aware validators exist elsewhere in this codebase, `IssueReturnRefundCommandValidator`/`UpdateReturnStatusCommandValidator` are the precedent for validator-does-shape-only, handler-does-existence-checks). Creates the `Product` (`IsPublished = false`) and its one-time `Stock` row (`Quantity = InitialStock`) in the same `SaveChangesAsync`. No cache invalidation needed — an unpublished product can't appear in any cached (published-only) query result.
- [x] Task 5: `Application/Catalogue/Commands/UpdateProductCommand.cs` + Handler + Validator (AC #2, #4, #5, #6). Updatable fields: `Name`, `Description`, `PriceInCents`, `CategoryId`, `Material`, `Color`, `Dimensions` — deliberately **not** `IsPublished` (Story 6.5's `PATCH /publish`) and **not** stock quantity (Story 6.4's `PATCH /stock`), keeping this story's write surface to exactly what its own AC lists. Calls `IProductCatalogueService.InvalidateCatalogueCacheAsync()` after `SaveChangesAsync` (AC #2). Returns `AdminProductDto`. 404s (via `NotFoundException`) if the product doesn't exist or is already soft-deleted — a deleted product is inert to every admin mutation here, not just public reads.
- [x] Task 6: `Application/Catalogue/Commands/DeleteProductCommand.cs` + Handler + Validator (AC #3, #4, #6). Sets `IsDeleted = true`, invalidates the catalogue cache (implicit in AC #3's "hidden from catalogue" — if the product was published and cached, its cache entries must be dropped same as an update would). Idempotent: deleting an already-deleted product is a no-op success (204), not a 404 — matches standard DELETE idempotency, and avoids a confusing error on a retried request.
- [x] Task 7: `Web/Endpoints/AdminProducts.cs` — `POST /api/v1/admin/products` (201, `{ id }`), `PUT /api/v1/admin/products/{id}` (200, `AdminProductDto`), `DELETE /api/v1/admin/products/{id}` (204). `.RequireAuthorization()` at the route-group level (proves "authenticated as someone"), the real admin-role gate is each command's own `[Authorize(Roles = Roles.Administrator)]` enforced by `AuthorizationBehaviour` — same split as `AdminOrders.cs`/`AdminReturns.cs`.
- [x] Task 8: AC #6 ("logged with admin user ID and timestamp") is already fully satisfied by the pre-existing `AuditableEntityInterceptor`, which stamps `CreatedBy`/`Created`/`LastModifiedBy`/`LastModified` on every `Added`/`Modified` `BaseAuditableEntity` — `Product` already is one. No new logging code needed; verified, not built.
- [x] Task 9: Unit tests — `CreateProductCommandHandlerTests`, `UpdateProductCommandHandlerTests`, `DeleteProductCommandHandlerTests` (InMemory EF Core provider, same pattern as other command handler tests this codebase already has), plus validator tests for the price/name/category shape rules (AC #5).

## Dev Notes

### Why a dedicated `IsDeleted` flag instead of reusing `IsPublished`

Considered and rejected: setting `IsPublished = false` on delete, since every catalogue read already filters on it and it would need zero new query changes. Rejected once Story 6.5's scope became clear while reading ahead in the epic — 6.5 gives `IsPublished` its own dedicated `PATCH /admin/products/{id}/publish` endpoint with its own guard (must have ≥1 image). If "deleted" and "unpublished" shared one flag, an admin republishing a soft-deleted product through that future endpoint would silently undelete it — two unrelated operations (one destructive, one routine) would collide on the same bit. A separate `IsDeleted` costs five extra `!p.IsDeleted` filter clauses in `ProductCatalogueService` (Task 2) but keeps the two concepts independent, which is what "soft-deleted... not physically removed" in AC #3 actually implies (a durable, distinct state, not a transient toggle Story 6.5 will flip back and forth).

### Why POST needs no cache invalidation but PUT and DELETE do

`ProductCatalogueService`'s every read path filters `p.IsPublished` (now `&& !p.IsDeleted`). A freshly created product is always `IsPublished = false`, so it cannot be present in any cached result set — nothing to invalidate. An update or delete can affect a product that IS currently published and cached (update changes its visible fields; delete removes it from view entirely), so both call `InvalidateCatalogueCacheAsync()`. AC #2 states this explicitly for PUT; AC #3 doesn't say it for DELETE in as many words, but "hidden from catalogue" is meaningless without it if the product was cached — treated as the same implicit requirement, not a scope addition.

### Scope boundaries versus Stories 6.2/6.4/6.5

This story's endpoints do not touch: product images (6.2 — `POST .../images`), CSV import (6.3), stock *quantity* changes after creation (6.4 — `PATCH .../stock`; this story's `InitialStock` only ever creates the Stock row once), or publish/unpublish and category CRUD (6.5 — `PATCH .../publish`, `POST /admin/categories`). `UpdateProductCommand` intentionally excludes `IsPublished` and stock quantity from its updatable fields for this reason.

## Project Structure Notes

New: `Application/Catalogue/Models/AdminProductDto.cs`, `Application/Catalogue/Commands/{CreateProductCommand,UpdateProductCommand,DeleteProductCommand}.cs` (+ Handlers + Validators), `Web/Endpoints/AdminProducts.cs`, migration for `Product.IsDeleted`, unit tests under `tests/Application.UnitTests/Catalogue/Commands/`. Modified: `Domain/Entities/Product.cs`, `Infrastructure/Catalogue/ProductCatalogueService.cs` (5 query filters).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 6.1 acceptance criteria (Epic 6 section, line ~1000), and Stories 6.2/6.4/6.5 read ahead to establish this story's exact scope boundary.
- `backend/MonEcommerce/src/Web/Endpoints/AdminOrders.cs`, `AdminReturns.cs` — the `.RequireAuthorization()` route-group + per-command `[Authorize(Roles = Roles.Administrator)]` split this story follows.
- `backend/MonEcommerce/src/Infrastructure/Data/Interceptors/AuditableEntityInterceptor.cs` — already satisfies AC #6 for any `BaseAuditableEntity`, including `Product`.
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` — every existing public catalogue read path, all five needing the new `!IsDeleted` filter.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Read ahead into Stories 6.2/6.4/6.5's own ACs before writing `UpdateProductCommand` to establish this story's exact write-surface boundary — confirmed `IsPublished` (6.5's `PATCH /publish`) and stock quantity (6.4's `PATCH /stock`) are each owned by a dedicated future endpoint, not this one's generic `PUT`.
- Chose a dedicated `Product.IsDeleted` flag over reusing `IsPublished` for the same reason: 6.5's future publish-toggle endpoint would otherwise be able to silently resurrect a soft-deleted product by flipping the one shared bit back to `true`.

### Completion Notes List

- `POST /api/v1/admin/products`, `PUT /api/v1/admin/products/{id}`, `DELETE /api/v1/admin/products/{id}` all implemented and admin-role gated via `[Authorize(Roles = Roles.Administrator)]` on each command (AC #4).
- `Product.IsDeleted` (new bool, migration `AddProductIsDeleted`) is a dedicated soft-delete flag, independent of `IsPublished` — see Dev Notes for the reasoning. All five `ProductCatalogueService` read paths (`GetProductsAsync`, `GetProductByIdAsync`, `GetSearchSuggestionsAsync`, `GetSimilarProductsAsync`, `GetSitemapEntriesAsync`) now also filter `!p.IsDeleted`.
- `UpdateProductCommand`/`DeleteProductCommand` both call `IProductCatalogueService.InvalidateCatalogueCacheAsync()` after their `SaveChangesAsync` (AC #2, and the same implicit requirement for AC #3). `CreateProductCommand` does not — an unpublished product can never be in a cached (published-only) result set, so there's nothing to invalidate.
- AC #5 (price validated as a positive integer, never negative or zero) enforced via `RuleFor(x => x.PriceInCents).GreaterThan(0)` in both `CreateProductCommandValidator` and `UpdateProductCommandValidator`.
- AC #6 (mutations logged with admin user ID and timestamp) required no new code — `Product : BaseAuditableEntity` is already stamped by the pre-existing `AuditableEntityInterceptor` on every `Added`/`Modified` save. Verified, not built.
- `DeleteProductCommandHandler` is idempotent by design: looking up the product without an `!IsDeleted` filter and returning early (204, no redundant cache invalidation) if already deleted, rather than throwing 404 on a retried request.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 253/253 Application.UnitTests passing, including 20 new tests (command handlers, validators, and two new `ProductCatalogueServiceTests` regression cases proving a published-but-soft-deleted product stays excluded from every public read). `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test/migrate on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Application/Catalogue/Models/AdminProductDto.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Commands/CreateProductCommand.cs` (+ `CreateProductCommandHandler.cs`, `CreateProductCommandValidator.cs`)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/UpdateProductCommand.cs` (+ `UpdateProductCommandHandler.cs`, `UpdateProductCommandValidator.cs`)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/DeleteProductCommand.cs` (+ `DeleteProductCommandHandler.cs`, `DeleteProductCommandValidator.cs`)
- `backend/MonEcommerce/src/Web/Endpoints/AdminProducts.cs`
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260731012932_AddProductIsDeleted.cs` (+ `.Designer.cs`, snapshot update)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/CreateProductCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/CreateProductCommandValidatorTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/UpdateProductCommandHandlerTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/UpdateProductCommandValidatorTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/DeleteProductCommandHandlerTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Domain/Entities/Product.cs` (`IsDeleted` field)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (5 query filters)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/ProductCatalogueServiceTests.cs` (2 new regression tests + `SeedProduct` helper's `isDeleted` parameter)
