# Story 6.2: Gestion des Images Produit

Status: done

## Story

As an administrator,
I want to upload multiple photos for each product via Cloudinary,
so that product pages display high-quality WebP galleries optimized for fast loading.

## Acceptance Criteria

1. Given an image file is uploaded to `POST /api/v1/admin/products/{id}/images`, when the upload is processed, then the image is stored in Cloudinary with automatic WebP conversion and resizing, and the CDN URL is saved to the product's image list and returned.
2. Given multiple images exist for a product, when the admin reorders them via `PATCH /api/v1/admin/products/{id}/images/order`, then the new display order is persisted and reflected on the public product page.
3. Given a product has no images, when an admin tries to publish it, then a `422` error is returned: "Au moins une image est requise pour publier un produit". **Not implementable by this story** — see Dev Notes' "Scope boundary versus Story 6.5".
4. Individual images can be deleted via `DELETE /api/v1/admin/products/{id}/images/{imageId}`.
5. Cloudinary transformations enforce ratio 3:4 and max width 1200px.
6. Upload progress is shown in the admin UI. **Not implementable by this story** — no admin frontend exists yet in this codebase (backend-only precedent, same as every other admin story this sprint: 5.2/5.3's `AdminOrders`/`AdminReturns`, 6.1's `AdminProducts`).

## Tasks / Subtasks

- [x] Task 1: `Domain/Entities/ProductImage.cs` gains `PublicId` (string) — the Cloudinary asset id needed to actually delete an image (AC #4); `FileUploadResult` already returns it, but `CreateReturnRequestCommandHandler` (Story 5.1) never persisted it since return photos are never deleted via the Cloudinary API. `Infrastructure/Data/Configurations/ProductImageConfiguration.cs` + migration.
- [x] Task 2: Extend `IFileStorageService.UploadAsync` with a new `ImageTransformPreset preset = ImageTransformPreset.None` parameter (new enum, `Application/Common/Interfaces/IFileStorageService.cs`: `None`, `ProductGallery`). `CloudinaryFileStorageService` currently hardcodes one `Transformation().FetchFormat("webp").Quality("auto")` shared by every caller — `ProductGallery` adds `AspectRatio("3:4").Crop("fill").Width(1200)` on top (AC #5) without forcing Story 5.1's return-photo uploads into the same 3:4 crop. Update `CreateReturnRequestCommandHandler`'s one existing call site (named `ct:` argument, so the new optional `preset` parameter doesn't shift its positional `cancellationToken` argument) and its test's `Setup(...)` signature.
- [x] Task 3: `Application/Catalogue/Models/ProductImageUpload.cs` (`Stream Content, string FileName)`) and `ProductImageDto.cs` (`Guid Id, string Url, int DisplayOrder`) — same Application-layer file-upload projection pattern as Story 5.1's `ReturnPhotoUpload` (no `IFormFile` reference outside `Web`).
- [x] Task 4: `Application/Catalogue/Commands/AddProductImageCommand.cs` + Handler + Validator (AC #1). `[Authorize(Roles = Roles.Administrator)]`. 404s if the product doesn't exist or is soft-deleted (same convention as Story 6.1's `UpdateProductCommandHandler`). Uploads via `IFileStorageService` with `ImageTransformPreset.ProductGallery`, folder `"products"`. `DisplayOrder` = current image count for that product (append to the end). Invalidates the catalogue cache — a published product's gallery is part of its cached `ProductDetailDto`.
- [x] Task 5: `Application/Catalogue/Commands/ReorderProductImagesCommand.cs` + Handler + Validator (AC #2). Takes the full ordered list of `ImageId`s; the handler validates the set exactly matches the product's existing image ids (a `ValidationException` otherwise — mismatched/partial/foreign ids are a client error, not a 404 or silent partial reorder), then assigns `DisplayOrder` = each id's index in the given order. Invalidates the catalogue cache.
- [x] Task 6: `Application/Catalogue/Commands/DeleteProductImageCommand.cs` + Handler + Validator (AC #4). Looks the image up scoped by **both** `ProductId` and `ImageId` (IDOR-safe query, established convention — a stray/foreign `imageId` under the wrong product 404s, doesn't silently touch it). Calls `IFileStorageService.DeleteAsync(publicId)` before removing the DB row and saving — if the Cloudinary call throws, nothing in the DB changes, keeping Cloudinary and Postgres from drifting out of sync. Invalidates the catalogue cache.
- [x] Task 7: Extend `Web/Endpoints/AdminProducts.cs` (not a new endpoint-group class — these are nested resources under the same `/api/v1/admin/products` prefix Story 6.1 already owns) with `POST {id}/images` (multipart, same `IFormFile` → Application-layer-record conversion pattern as `Account.CreateReturnRequest`), `PATCH {id}/images/order`, `DELETE {id}/images/{imageId}`.
- [x] Task 8: Unit tests — `AddProductImageCommandHandlerTests`, `ReorderProductImagesCommandHandlerTests`, `DeleteProductImageCommandHandlerTests`, plus validator tests. Extend `CloudinaryFileStorageService`'s... no dedicated unit tests exist for it today (it's a thin wrapper over the Cloudinary SDK, untested by this codebase already — not introducing a new gap, just not closing a pre-existing one out of scope for this story).

## Dev Notes

### Scope boundary versus Story 6.5 (AC #3)

AC #3 describes what happens when an admin "tries to publish" a product with no images — but the only thing that can "try to publish" a product is `PATCH /admin/products/{id}/publish`, which is Story 6.5's endpoint, not built yet (Story 6.1 deliberately excluded `IsPublished` from its own `PUT`, see 6.1's Dev Notes). There is nothing in this codebase today that can trigger AC #3's precondition, so it cannot be implemented or tested by this story — doing so here would mean writing dead code with no caller until 6.5 exists, or building a premature, duplicate publish endpoint out of turn. Flagged explicitly rather than silently dropped: **Story 6.5 must add this exact guard** (`if (!product.Images.Any()) throw new ValidationException(...)` with message "Au moins une image est requise pour publier un produit", checked when its `PATCH /publish` sets `IsPublished = true`) when it's implemented.

### Scope boundary versus admin frontend (AC #6)

"Upload progress is shown in the admin UI" requires an admin section of the Angular app, which doesn't exist anywhere in this codebase yet — no story in Epics 6 or 7 so far has built one; `AdminOrders`/`AdminReturns`/`AdminProducts` are all backend-only APIs with no corresponding frontend. Flagged as a known gap, not silently dropped, consistent with how every other admin-facing story this sprint has been scoped.

### Why `ImageTransformPreset` instead of a Cloudinary-specific parameter on `IFileStorageService`

`IFileStorageService` is an Application-layer interface with no reference to `CloudinaryDotNet` — passing a raw `Transformation` object through it would leak an Infrastructure type across the boundary the same way `ReturnPhotoUpload`/`WebhookEvent` were deliberately kept implementation-agnostic. A small enum (`None`/`ProductGallery`) keeps the interface decoupled while still letting `CloudinaryFileStorageService` decide exactly what "ProductGallery" means in Cloudinary terms.

## Project Structure Notes

New: `Application/Catalogue/Models/{ProductImageUpload,ProductImageDto}.cs`, `Application/Catalogue/Commands/{AddProductImageCommand,ReorderProductImagesCommand,DeleteProductImageCommand}.cs` (+ Handlers + Validators), migration for `ProductImage.PublicId`, unit tests under `tests/Application.UnitTests/Catalogue/Commands/`. Modified: `Domain/Entities/ProductImage.cs`, `Application/Common/Interfaces/IFileStorageService.cs`, `Infrastructure/ExternalServices/CloudinaryFileStorageService.cs`, `Infrastructure/Data/Configurations/ProductImageConfiguration.cs`, `Web/Endpoints/AdminProducts.cs`, `Application/Returns/Commands/CreateReturnRequestCommandHandler.cs` (call-site update only, no behavior change).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 6.2 acceptance criteria (Epic 6 section, line ~1026); Story 6.5 read ahead to confirm AC #3's actual owner.
- `backend/MonEcommerce/src/Application/Returns/Models/ReturnPhotoUpload.cs`, `Web/Endpoints/Account.cs`'s `CreateReturnRequest` — the established `IFormFile` → Application-record conversion pattern this story reuses for image uploads.
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/CloudinaryFileStorageService.cs` — the one existing Cloudinary integration, extended (not replaced) by this story.
- `_bmad-output/implementation-artifacts/6-1-crud-fiches-produits.md` — established the `IsPublished`/stock-quantity exclusion precedent this story's AC #3 deferral follows.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `IFileStorageService.UploadAsync`'s new optional `preset` parameter sits between `folder` and `ct` — the one pre-existing positional call site (`CreateReturnRequestCommandHandler`) would have silently tried to bind its `CancellationToken` argument to the new parameter's position; caught at compile time (no implicit `CancellationToken` → `ImageTransformPreset` conversion exists), fixed with a named `ct:` argument instead of reordering the parameter list.
- Confirmed via the Cloudinary SDK's fluent `Transformation` API that `AspectRatio("3:4").Crop("fill").Width(1200)` composes into a single transformation step (crop=fill with both an aspect ratio and a width) rather than needing two chained steps — matches AC #5 without extra complexity.

### Completion Notes List

- `POST/PATCH/DELETE /api/v1/admin/products/{id}/images...` all implemented as nested resources on the existing `AdminProducts` endpoint group (Story 6.1), admin-role gated via `[Authorize(Roles = Roles.Administrator)]` on each command.
- `ProductImage.PublicId` (new, migration `AddProductImagePublicId`) closes a gap Story 5.1 left open (return photos never persisted their Cloudinary public id since they're never deleted) — product images need it for AC #4's delete.
- `IFileStorageService.UploadAsync` gained a decoupled `ImageTransformPreset` enum (`None`/`ProductGallery`) rather than a raw Cloudinary `Transformation` parameter, keeping the Application-layer interface free of any Infrastructure/SDK type — `CloudinaryFileStorageService` is the only place that knows what `ProductGallery` means (3:4 crop, max width 1200px, on top of the existing WebP/auto-quality transform every upload already gets).
- `AddProductImageCommand`/`ReorderProductImagesCommand`/`DeleteProductImageCommand` all call `IProductCatalogueService.InvalidateCatalogueCacheAsync()` — not explicitly required by AC #1/#2/#4's wording, but a published product's gallery is part of its cached `ProductDetailDto`, so skipping this would let a stale image list linger for up to the 5-minute cache TTL. Same reasoning already used for Story 6.1's `DeleteProductCommand`.
- `ReorderProductImagesCommandHandler` requires the submitted `ImageIds` set to exactly match the product's existing images (`ValidationException`/422 otherwise) — deliberately rejects partial or foreign-id reorders rather than attempting a best-effort partial application.
- `DeleteProductImageCommandHandler` scopes its lookup by both `ProductId` and `ImageId` (IDOR-safe convention) and deletes the Cloudinary asset before touching the DB row, so a Cloudinary failure never leaves the database out of sync with what's actually still hosted.
- AC #3 (block publishing an image-less product) and AC #6 (upload progress in the admin UI) are **not implemented by this story** — see Dev Notes for why (Story 6.5 owns the publish endpoint; no admin frontend exists yet anywhere in this codebase). Both are explicitly flagged, not silently dropped.
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 272/272 Application.UnitTests passing, including 19 new tests (3 command handlers × handler+validator tests, plus the one pre-existing `CreateReturnRequestCommandHandlerTests` mock signature updated for the new `preset` parameter). `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test/migrate on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff).

### File List

**New:**
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductImageUpload.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductImageDto.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Commands/AddProductImageCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/ReorderProductImagesCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Application/Catalogue/Commands/DeleteProductImageCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260801183922_AddProductImagePublicId.cs` (+ `.Designer.cs`, snapshot update)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/{AddProductImageCommandHandlerTests,AddProductImageCommandValidatorTests,ReorderProductImagesCommandHandlerTests,ReorderProductImagesCommandValidatorTests,DeleteProductImageCommandHandlerTests,DeleteProductImageCommandValidatorTests}.cs`

**Modified:**
- `backend/MonEcommerce/src/Domain/Entities/ProductImage.cs` (`PublicId` field)
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/ProductImageConfiguration.cs`
- `backend/MonEcommerce/src/Application/Common/Interfaces/IFileStorageService.cs` (`ImageTransformPreset` enum + parameter)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/CloudinaryFileStorageService.cs` (preset-aware transformation)
- `backend/MonEcommerce/src/Application/Returns/Commands/CreateReturnRequestCommandHandler.cs` (call-site update only)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Commands/CreateReturnRequestCommandHandlerTests.cs` (mock signature update)
- `backend/MonEcommerce/src/Web/Endpoints/AdminProducts.cs` (3 new nested image routes)
