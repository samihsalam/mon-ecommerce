# Story 6.5: Catégories & Publication

Status: done

## Story

As an administrator,
I want to organize products into categories/subcategories and control their public visibility,
so that the catalogue is well-structured and product launches can be managed.

## Acceptance Criteria

1. Given a category name, when `POST /api/v1/admin/categories` is called, then the category is created with an auto-generated URL slug from the name.
2. Given a subcategory with a parent category ID, when `POST /api/v1/admin/categories` is called with `parentId`, then the subcategory is created and appears nested under its parent in the catalogue filters.
3. Given a product with at least one image, when `PATCH /api/v1/admin/products/{id}/publish` is called with `{ isPublished: true }`, then the product becomes visible on the public catalogue immediately.
4. Given a published product is unpublished, when `PATCH /api/v1/admin/products/{id}/publish` is called with `{ isPublished: false }`, then the product is hidden from the public catalogue and the Redis cache is invalidated.
5. Categories appear as filter options in the public catalogue.
6. Slug generation follows kebab-case: "Sacs Mode" → `sacs-mode`.
7. Categories cannot be deleted if they contain published products.

## Tasks / Subtasks

- [x] Task 1: `Application/Catalogue/Commands/CreateCategoryCommand.cs` + Handler + Validator (AC #1, #2, #6). `[Authorize(Roles = Roles.Administrator)]`. `Name` (required), `ParentId` (optional Guid, validated to reference an existing category — `NotFoundException` otherwise, same convention as `CategoryId` in Story 6.1's `CreateProductCommand`). Slug via the existing `SlugHelper.Slugify(name)` (Story 3.5/sitemap) — reused, not reinvented, and already produces exactly AC #6's kebab-case ("Sacs Mode" → `sacs-mode`). `Category.Slug` has a pre-existing unique index (`CategoryConfiguration.cs`, Story 1.3) — a collision throws `ConflictException`, no auto-disambiguation (e.g. appending `-2`) attempted; the AC doesn't ask for it and it would need its own design decisions (retry count, suffix scheme) out of proportion for this story.
- [x] Task 2: `Application/Catalogue/Commands/DeleteCategoryCommand.cs` + Handler + Validator (AC #7). `[Authorize(Roles = Roles.Administrator)]`. Not explicit in any AC's own Given/When/Then, but implied by "categories cannot be deleted if..." — there is no delete behavior to test that rule against otherwise. Three guards, each a `ConflictException` (409) rather than letting a raw FK violation surface as an unhandled 500: (a) category has child categories (`Category.ParentId`'s FK is `DeleteBehavior.Restrict`, Story 1.3) — blocked regardless of AC #7's wording, since the DB would reject it anyway; (b) category has published products — AC #7's literal condition; (c) category has ANY non-deleted products at all, published or not — also blocked by the same `Restrict` FK on `Product.CategoryId`, so an unpublished-only category would 500 without this second check. (b) is checked and reported first since it's the AC's own named business reason; (c) is the safety net.
- [x] Task 3: `Application/Catalogue/Commands/PublishProductCommand.cs` + Handler + Validator (AC #3, #4) — **implements the "must have ≥1 image to publish" guard Stories 6.1 and 6.2 both explicitly deferred to this story** (see their Dev Notes). `[Authorize(Roles = Roles.Administrator)]`. `IsPublished = true` with zero images → `ValidationException` (422), exact message "Au moins une image est requise pour publier un produit" (Story 6.2's AC #3's own wording). Invalidates the catalogue cache in both directions (publish and unpublish) — AC #4 states it explicitly for unpublish; publish needs it too, symmetric with every other catalogue-affecting mutation in Stories 6.1/6.2/6.4, otherwise a cached "products list" response from just before the publish wouldn't show the now-visible product until its 5-minute TTL expires.
- [x] Task 4: `Application/Catalogue/Models/CategorySummaryDto.cs` gains `ParentId` (`Guid?`) — AC #2's "appears nested under its parent in the catalogue filters" needs the parent relationship in the data a filter UI would build a hierarchy from; the existing DTO was flat (`Id, Name, Slug` only). `ProductCatalogueService.GetCategoriesAsync`'s projection updated to include it. AC #5 ("categories appear as filter options in the public catalogue") is **pre-existing, verified not rebuilt** — `GET /api/v1/products/categories` (`Web/Endpoints/Products.cs`, public/anonymous) already calls this exact query; this story only had to make its DTO nesting-capable.
- [x] Task 5: `Application/Catalogue/Models/CategoryDto.cs` (admin response shape: `Id, Name, Slug, ParentId`).
- [x] Task 6: `Web/Endpoints/AdminCategories.cs` (new endpoint-group class — categories are a distinct resource from products, unlike Story 6.2/6.3/6.4's nested `/products/{id}/...` routes) — `POST /api/v1/admin/categories`, `DELETE /api/v1/admin/categories/{id}`. `Web/Endpoints/AdminProducts.cs` — `PATCH {id}/publish`.
- [x] Task 7: Unit tests — `CreateCategoryCommandHandlerTests` (slug generation, parent linking, slug collision, unknown parent), `DeleteCategoryCommandHandlerTests` (blocked by children, blocked by published products, blocked by unpublished products, succeeds when empty), `PublishProductCommandHandlerTests` (blocks publish with no images, allows publish with ≥1 image, allows unpublish unconditionally, invalidates cache both directions), validator tests.

## Dev Notes

### This story closes the "publish guard" gap Stories 6.1 and 6.2 both flagged

Story 6.1 deliberately excluded `IsPublished` from `UpdateProductCommand`, stating Story 6.5 owns it exclusively. Story 6.2 stated its AC #3 (block publishing an image-less product) "cannot be implemented... doing so here would mean writing dead code with no caller until 6.5 exists." Both statements are resolved here: `PublishProductCommand` is the one and only place `IsPublished` is set from an admin action, and it carries exactly the guard 6.2 described, word for word.

### Category deletion guards beyond AC #7's literal wording

AC #7 only names "published products" as a deletion blocker. Two more guards were added because the existing schema (Story 1.3) already makes them unavoidable at the database level: `Category.ParentId` and `Product.CategoryId` are both `DeleteBehavior.Restrict` foreign keys. Skipping the "has children" or "has any (including unpublished) products" checks wouldn't make those categories deletable — it would just mean the failure surfaces as an unhandled `DbUpdateException` → generic 500 instead of a clear `ConflictException` → 409 with a message explaining why. Checking proactively turns an accidental crash into an intentional, documented business rule.

### No slug auto-disambiguation on collision

`Category.Slug` has had a unique index since Story 1.3. This story returns `ConflictException` on a collision rather than inventing a suffixing scheme (`sacs-mode-2`, etc.) — the AC never asks for one, and auto-generating a *different* slug than what the admin would see reflected back could itself be surprising/undesirable UX. Left as a clear, admin-correctable error instead.

## Project Structure Notes

New: `Application/Catalogue/Commands/{CreateCategoryCommand,DeleteCategoryCommand,PublishProductCommand}.cs` (+ Handlers + Validators), `Application/Catalogue/Models/CategoryDto.cs`, `Web/Endpoints/AdminCategories.cs`, unit tests under `tests/Application.UnitTests/Catalogue/Commands/`. Modified: `Application/Catalogue/Models/CategorySummaryDto.cs`, `Infrastructure/Catalogue/ProductCatalogueService.cs` (`GetCategoriesAsync` projection), `Web/Endpoints/AdminProducts.cs` (`PATCH {id}/publish`).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 6.5 acceptance criteria (Epic 6 section, line ~1108) — the last story in Epic 6.
- `_bmad-output/implementation-artifacts/6-1-crud-fiches-produits.md` — excluded `IsPublished` from its own `UpdateProductCommand`, naming this story as the owner.
- `_bmad-output/implementation-artifacts/6-2-gestion-des-images-produit.md` — deferred its own AC #3 (block publish without images) here verbatim.
- `backend/MonEcommerce/src/Application/Common/Utilities/SlugHelper.cs` — the existing slugify implementation this story reuses for category slugs, not a second copy.
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` — the pre-existing public `GET /categories` endpoint AC #5 was verified against.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Confirmed `Category.Slug` (unique index) and `Category.ParentId`/`Product.CategoryId` (both `DeleteBehavior.Restrict`) were already in place since Story 1.3 — no schema/migration changes needed anywhere in this story, only new commands/queries and one DTO field addition.
- Verified `GET /api/v1/products/categories` (public, anonymous, `Web/Endpoints/Products.cs`) already existed and already called `GetCategoriesQuery`/`ProductCatalogueService.GetCategoriesAsync` — AC #5 needed no new endpoint, only `CategorySummaryDto` gaining `ParentId` so a filter UI can build nesting.

### Completion Notes List

- `POST /api/v1/admin/categories` (name → kebab-case slug via the existing `SlugHelper`, optional `parentId`) and `DELETE /api/v1/admin/categories/{id}` implemented on a new `AdminCategories` endpoint group (categories are a distinct resource from products, unlike Stories 6.2–6.4's nested `/products/{id}/...` routes).
- `PATCH /api/v1/admin/products/{id}/publish` is the one and only place `IsPublished` is set — this closes the gap both Story 6.1 (excluded `IsPublished` from its `UpdateProductCommand`) and Story 6.2 (deferred its AC #3 image guard here verbatim, exact same message: "Au moins une image est requise pour publier un produit") explicitly named this story as owning.
- `DeleteCategoryCommandHandler` enforces three guards, not just AC #7's literal "published products" one — a category with children or with any (including unpublished) products would be rejected by the pre-existing `DeleteBehavior.Restrict` foreign keys regardless, so the extra checks turn what would otherwise be an unhandled `DbUpdateException` → 500 into a clear `ConflictException` → 409.
- `CategorySummaryDto` gained `ParentId` (non-breaking additive change — verified no test or other code constructs it positionally without accounting for it) so `GET /api/v1/products/categories`'s existing, pre-built public endpoint can support nested filter rendering.
- All catalogue-cache-affecting mutations in this story (`CreateCategoryCommand`, `DeleteCategoryCommand`, `PublishProductCommand` in both directions) call `InvalidateCatalogueCacheAsync()`, consistent with every prior Epic 6 story.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 317/317 Application.UnitTests passing, including 19 new tests across `CreateCategoryCommandHandlerTests`, `DeleteCategoryCommandHandlerTests`, `PublishProductCommandHandlerTests`, and their validators. No migration needed. `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).
- This is the last story in Epic 6 — all five stories (6.1–6.5) are now done.

### File List

**New:**
- `backend/MonEcommerce/src/Application/Catalogue/Commands/CreateCategoryCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/DeleteCategoryCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/PublishProductCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Application/Catalogue/Models/CategoryDto.cs`
- `backend/MonEcommerce/src/Web/Endpoints/AdminCategories.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/{CreateCategoryCommandHandlerTests,CreateCategoryCommandValidatorTests,DeleteCategoryCommandHandlerTests,DeleteCategoryCommandValidatorTests,PublishProductCommandHandlerTests,PublishProductCommandValidatorTests}.cs`

**Modified:**
- `backend/MonEcommerce/src/Application/Catalogue/Models/CategorySummaryDto.cs` (`ParentId` field)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (`GetCategoriesAsync` projection)
- `backend/MonEcommerce/src/Web/Endpoints/AdminProducts.cs` (`PATCH {id}/publish`)
