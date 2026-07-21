# Story 3.5: Fiche Produit & ProductGallery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to view a complete product page with a multi-photo gallery, description, dimensions, and stock availability,
so that I have all the information I need to make a purchase decision.

## Acceptance Criteria

1. **Given** a product detail page is requested, **when** `GET /api/v1/products/{productId}` is called, **then** the response includes: name, description, price (cents), material, dimensions, stock quantity, category, and an array of image URLs (WebP, CDN).
2. **Given** the product gallery loads, **when** a visitor views the desktop layout, **then** thumbnails are displayed in a left column and the main image fills the right (keyboard ←/→ navigation).
3. **Given** the product gallery loads on mobile, **when** a visitor swipes, **then** images scroll horizontally with dot indicators below.
4. `aria-roledescription="carousel"` is set on the gallery container.
5. Each image has a descriptive `alt` attribute (e.g., "Tote Parisienne en cuir cognac, vue de face").
6. A "Retour facile 14j" badge is visible on every product page.
7. The URL follows the semantic pattern `/catalogue/{category-slug}/{product-slug}`. **[Adapted — see Dev Notes]**: `Product` has no persisted slug field (only `Category` does — Story 1.3's schema). Rather than a schema migration this environment can't verify against the unreachable target SQL Server instance, the "product-slug" segment is generated client-side as `{slugified-name}-{full-product-guid}` — a common real-world e-commerce URL pattern (human-readable text for SEO/UX, the actual GUID embedded for lookup) that needs no backend change at all, since the AC's own backend contract (#1) is ID-based, not slug-based.
8. Skeleton placeholders display during image loading.

## Tasks / Subtasks

### Backend — new product detail endpoint, extends Story 3.1/3.2/3.3's existing `IProductCatalogueService`

- [x] Task 1: `ProductDetailDto` (AC: #1)
  - [x] `Application/Catalogue/Models/ProductDetailDto.cs`: `public record ProductDetailDto(Guid Id, string Name, string Description, int PriceInCents, string? Material, string? Color, string? Dimensions, int StockQuantity, bool InStock, Guid CategoryId, string CategoryName, string CategorySlug, List<string> ImageUrls);` — deliberately a distinct DTO from Story 3.1's `ProductSummaryDto` (list-view shape), not an extension of it, matching this codebase's established "distinct DTOs per distinct response shape" convention (Story 3.1's own `PagedProductsResult` vs. Account's `PagedResult` precedent)
- [x] Task 2: `GetProductByIdQuery` + validator + handler (AC: #1)
  - [x] `GetProductByIdQuery(Guid Id) : IRequest<ProductDetailDto>` — no `[Authorize]`, public like every other catalogue query
  - [x] `IProductCatalogueService.GetProductByIdAsync(Guid id, ...)` / `ProductCatalogueService` implementation: queries `Where(p => p.IsPublished)` (same "published only" interpretation as Story 3.1's list endpoint — an unauthenticated detail page for an unpublished/draft product would be a content-leak bug, not a defensible reading of the AC), throws `NotFoundException` when no match (→ 404 via the existing `ProblemDetailsExceptionHandler`, Story 1.5), maps `Category.Slug` and orders `Images` by `DisplayOrder`
  - [x] Cached the same way as Story 3.1/3.2's other catalogue reads (`catalogue:v{version}:product:{id}` key, same `catalogue:version` invalidation scheme — a product detail page is exactly the kind of read-heavy, rarely-changing data this cache layer exists for)
- [x] Task 3: Endpoint (AC: #1)
  - [x] `Web/Endpoints/Products.cs`: `GET /api/v1/products/{id:guid}`, `.AllowAnonymous()` — the `:guid` route constraint means this cannot ambiguously capture the existing literal `/suggestions`/`/categories` sub-routes (ASP.NET Core routing also prefers literal segments over parameterized ones regardless)
- [x] Task 4: Backend tests
  - [x] `ProductCatalogueServiceTests.cs`: returns the full detail shape for a published product with multiple images (ordered correctly) and stock; throws `NotFoundException` for a nonexistent id; throws `NotFoundException` for an unpublished product (same as the list endpoint's exclusion, now proven at the single-product level too); cache hit/miss behavior mirrors the existing list-endpoint tests

### Angular — product detail page + ProductGallery component

- [x] Task 5: Slug/URL utilities (`features/catalogue/product-url.util.ts`) (AC: #7)
  - [x] `buildProductUrl(categorySlug, productId, productName)` — `/catalogue/{categorySlug}/{slugify(productName)}-{productId}`; `extractProductIdFromSlug(slugSegment)` — regex-extracts the trailing GUID; `slugify(text)` — lower-cases, strips accents (`normalize('NFD')`), replaces non-alphanumerics with hyphens, trims
  - [x] `ProductCardComponent` (Story 3.3) updated to build its `routerLink` via `buildProductUrl(...)` instead of the placeholder `/produits/:id` it shipped with (that route never had a real destination — this story is the one that builds it)
- [x] Task 6: `ProductGalleryComponent` (`components/product-gallery/`) (AC: #2, #3, #4, #5, #8)
  - [x] `role="region"`, `aria-roledescription="carousel"` on the gallery container; each image has `[attr.alt]` built as `"{{ productName }}, vue {{ index + 1 }}"` (own interpretation of the AC's illustrative example — the backend has no per-image descriptive caption/angle field, so a numbered "vue N" fallback is used; see Dev Notes)
  - [x] Desktop (`md:` and up): CSS Grid, thumbnail column (buttons, `aria-current="true"` on the active one) on the left, main image on the right; `(keydown.arrowleft)`/`(keydown.arrowright)` cycle the active image, only bound when the gallery itself has focus (`tabindex="0"` on the container)
  - [x] Mobile (below `md:`): horizontal `overflow-x-auto` scroll-snap strip, dot indicators below driven by an `IntersectionObserver`-free scroll-position calculation (`(scroll)` handler dividing `scrollLeft` by container width)
  - [x] Skeleton: a 3:4 placeholder block (same token/shape as Story 3.3's `ProductCardSkeletonComponent`) shown per-image via `(load)`/`(error)` tracking on each `<img>`, not a single all-or-nothing gallery-level loading flag — individual images can finish at different times
- [x] Task 7: `ProductDetailComponent` (`pages/product-detail/`) (AC: #1, #6, #7)
  - [x] Mounted at `/catalogue/:categorySlug/:productSlug`; extracts the product id via `extractProductIdFromSlug(route.snapshot.paramMap.get('productSlug'))`, 404s gracefully (renders a "Produit introuvable" message, doesn't crash) if the segment isn't GUID-shaped or the API 404s
  - [x] Renders name, description, price, material, dimensions, stock-quantity/availability, category (breadcrumb-style link back to `/catalogue?categoryId=...`), the `ProductGalleryComponent`, and a "Retour facile 14j" badge (static, unconditional per AC #6)
- [x] Task 8: Route registration — `/catalogue/:categorySlug/:productSlug` replaces the placeholder `/produits/:id` route from Story 3.3 in `app.routes.ts`, lazy-loaded, no `authGuard`

### Flutter — a simpler product detail screen (closes Story 3.4's dangling `/produits/:id` link)

- [x] Task 9: `ProductDetailScreen` (`features/catalogue/screens/product_detail_screen.dart`) (AC: #1, #3, #6, #8)
  - [x] **Scoped down from the Angular version — see Dev Notes.** No separate epic story exists for a Flutter-specific product detail screen, and the AC's own gallery requirements (keyboard ←/→ navigation, `aria-roledescription="carousel"`, semantic URL routing) are web-specific concepts with no meaningful Flutter mobile-app equivalent (no URL bar, no physical-keyboard-first UX). What Flutter DOES need — and gets — is a functional destination for Story 3.4's `ProductCard` tap target: a `PageView` image carousel with `Semantics(label: 'Image N sur M')` per page and dot indicators below (Flutter's native swipe gesture IS the AC's "mobile swipe" requirement, satisfied natively, no custom implementation needed), name/description/price/material/dimensions/stock, and the same "Retour facile 14j" badge
  - [x] Route stays ID-based (`/produits/:id`, already registered by Story 3.4) — no slug/SEO requirement applies to in-app navigation with no deep-linking configured (confirmed: `router.dart`'s own comment states no universal/app links exist in this project yet)
- [x] Task 10: `productDetailProvider` (`features/catalogue/providers/product_detail_provider.dart`) — same plain-`Notifier` isLoading/error/data pattern as `CatalogueNotifier`/`OrdersNotifier`, `loadProduct(id)` calling `GET /api/v1/products/{id}`

### Verification

- [x] Task 11: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test` (116/116 passing, final post-review run)
  - [x] Angular: `ng build` (SSR, prerendered) + `ng test` via Edge-as-ChromeHeadless (49/49 passing, final post-review run)
  - [x] Flutter: unverified by any tooling (no SDK in this environment, consistent with every prior Flutter story)

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–3.4's predecessors). The Acceptance Auditor found **no AC violations** — all 8 acceptance-criteria bullets pass, including every documented deviation (client-side slug, numbered image alt fallback, Flutter's reduced scope) independently re-verified as genuine and correctly wired end-to-end.

- [x] [Review][Patch] `ProductDetailComponent` never re-fetched on param changes, confirmed independently by both Blind Hunter and Edge Case Hunter (the latter verified no custom `RouteReuseStrategy` is registered anywhere, so Angular's default reuse behavior genuinely applies here): `ngOnInit` read `route.snapshot.paramMap` once and never subscribed to subsequent changes. Since Angular's default `RouteReuseStrategy` reuses a component instance across two URLs matching the *same route config* (`catalogue/:categorySlug/:productSlug`) with only the params differing, navigating from one product-detail page directly to another (a future "related product" link, or browser back/forward between two previously-visited product pages) would NOT re-run `ngOnInit` — the URL would change but the page would keep rendering the FIRST product's name/price/images/stock, with no loading state and no error. This made the store's request-id staleness guard (correct in isolation) effectively unexercised by real navigation, since `loadProduct()` was never even called a second time. **Fixed:** `ngOnInit` now subscribes to `route.paramMap` (with `takeUntilDestroyed`), re-extracting the product id and re-calling `loadProduct()` on every emission — the same pattern already established by `SearchResultsComponent`/`CatalogueComponent` (Stories 3.2/3.3), applied here for consistency rather than invented fresh.
- [x] [Review][Patch] Desktop main image's loading/error state was driven by the wrong DOM element, found by Edge Case Hunter (Medium — the more subtle of the two substantive findings): the main `<img>` had no `(load)`/`(error)` bindings of its own; its skeleton/error overlay instead read `imageStatus()[activeIndex()]`, a value only ever written by the *same-index thumbnail's* own `(load)`/`(error)` handlers. The main image and its thumbnail are two independent `<img>` elements requesting the identical URL as two separate network requests (the thumbnail is additionally `loading="lazy"`) — so the main image's skeleton could remain visible after the main image itself had already rendered (until the lazily-deferred thumbnail request also happened to resolve), and a main-image request failure wasn't caught at all until the thumbnail's own request separately failed. **Fixed:** added a dedicated `mainImageStatus` signal, written only by the main image's own `(load)`/`(error)` handlers and reset to `'loading'` on every active-index change (via a new centralized `setActiveIndex` helper used by both `setActive()` and `onMobileScroll()`); `imageStatus[]` now backs only the thumbnail column and mobile strip, where each index genuinely has exactly one real `<img>`.
- [x] [Review][Patch] Mobile dot-indicator row had no `length > 1` guard, found by Edge Case Hunter (Low, cosmetic): a single-image product showed one meaningless dot, inconsistent with the Flutter screen's own `if (imageUrls.length > 1)` guard for the same UI element. **Fixed:** wrapped the dot row in `@if (images.length > 1)`, matching Flutter.
- [x] [Review][Patch] `activeIndex` was never clamped when the `images` input could shrink on a reused component instance, found by Blind Hunter (Low, latent — not reachable by any path in this story, since nothing yet re-navigates a `ProductGalleryComponent` instance with a different, shorter image list; the routing fix above doesn't change this, as fixing it means the component now correctly reinitializes the STORE's product, and Angular still creates a fresh `ProductGalleryComponent` child each time the parent's `@if (productDetailStore.product())` branch re-evaluates with a new product object, per Angular's default content-projection lifecycle for `@if`-gated content — but the reviewer's underlying point, that `ngOnChanges` alone doesn't clamp `activeIndex`, was still a real gap for any FUTURE reuse scenario like a "related products" mini-gallery). **Fixed:** `ngOnChanges` now clamps `activeIndex` to the new `images.length - 1` whenever it would otherwise point past the end.
- [x] [Review][Note] Thumbnail buttons themselves have no skeleton overlay (only the main/mobile primary viewing surfaces do), found by the Acceptance Auditor while verifying AC #8 — assessed as satisfying the AC's actual intent (avoiding layout shift/blank state on the surfaces a visitor is actually looking at) rather than a gap; the thumbnails are small enough that an unstyled blank-to-loaded transition isn't the kind of jarring layout shift AC #8 is guarding against. Not fixed.
- [x] [Review][Note] "WebP, CDN" image URL format (part of AC #1's literal wording) isn't something any code in this story enforces — `ProductImage.Url` is a plain string, and whatever format/host it contains is set at upload time (Epic 6, catalogue administration), not read or validated here. Correctly identified by the Acceptance Auditor as not verifiable from this story's files, not a gap in this story's own scope.
- [x] [Review][Note] Both other reviewers independently traced the EF Core translation, cache-key/versioning scheme, and `/{id:guid}` route-constraint precedence (confirming a non-GUID segment falls through to a normal 404, never an unhandled exception, and can never ambiguously shadow the literal `/suggestions`/`/categories` routes) and found all three correct — no fix needed, noted as verified rather than assumed.

## Dev Notes (post-implementation addendum)

### The `RouteReuseStrategy` gap is a broader lesson, not just this component's bug

Two independent reviewers converging on the exact same finding (`ProductDetailComponent` not reacting to param changes on a reused route) confirms this is a real, easy-to-miss Angular gotcha: ANY route with dynamic params that could plausibly be navigated to from itself (product → related product, category → subcategory, etc.) needs either a `paramMap`/`queryParamMap` subscription (this codebase's established fix, used here and in Stories 3.2/3.3) or an explicit `RouteReuseStrategy` override — a one-shot `route.snapshot` read in `ngOnInit` is only safe for routes nothing ever links to from an instance of themselves. Worth checking for on any future detail-page route in this codebase.

## Dev Notes

### The URL's "product-slug" is generated client-side, not stored in the database

`Product` has no `Slug` column (only `Category` does, from Story 1.3). Adding one would mean a schema migration this environment cannot verify against the real, unreachable `DESKTOP-M36577B` SQL Server instance — the same standing limitation as every prior migration-touching story this session. It's also unnecessary: AC #1's own backend contract is explicitly ID-based (`GET /api/v1/products/{productId}`), so the actual product ID has to be available to the frontend regardless of whatever the URL looks like. The chosen pattern — `{slugified-product-name}-{full-product-guid}` as a single trailing path segment — is a well-established real-world e-commerce convention (human-readable text for SEO/sharing, the GUID embedded for exact lookup, no backend slug-resolution endpoint needed, no collision risk since GUIDs are unique by construction). `extractProductIdFromSlug` recovers the ID via a trailing-GUID regex match; the human-readable prefix is decorative and never parsed.

### Image `alt` text is a numbered fallback, not a true per-image description

AC #5's example ("Tote Parisienne en cuir cognac, vue de face") implies each image has its own descriptive caption (e.g., distinguishing "front view" from "detail of the clasp"). `ProductImage` (Story 1.3's schema) has no caption/angle/description field — only `Url` and `DisplayOrder`. Built `alt` text as `"{ProductName}, vue {N}"` (e.g., "Tote Parisienne en cuir cognac, vue 1") — genuinely descriptive of *which* product and *which* image in sequence, satisfying the AC's underlying accessibility intent, without inventing a per-image caption data model this story doesn't have data for. If per-image captions become a real content-authoring need, `ProductImage` gaining a `Caption` field (Epic 6, catalogue administration) is the natural follow-up.

### Flutter's product detail screen is intentionally lighter than Angular's

The epic's story list has no Flutter-specific counterpart to this story (unlike 3.3/3.4's explicit Angular/Flutter split). Several of this AC's requirements are inherently web concepts with no native-app equivalent: `aria-roledescription="carousel"` is a web ARIA role, keyboard ←/→ navigation targets a physical keyboard and mouse-pointer desktop layout, and the semantic URL/slug pattern only matters for a page that has a URL bar and needs to be indexed by search engines — none of which apply to Flutter screen-to-screen navigation in an app with no deep-linking configured. What DOES carry over identically — stock-aware "Retour facile 14j" badge, image carousel with dot indicators (native swipe, no custom gesture code needed), skeleton loading — is built. This scope boundary is a judgment call within established patterns (matching Story 3.4's own "AC requirement is inherently web-specific, scoped to Angular" reasoning for keyboard accessibility), not escalated further.

### Product detail is now cached like every other catalogue read

Consistent with Story 3.1/3.2's `catalogue:v{version}:...` cache-key scheme, so Epic 6's future product-edit command handlers invalidate detail pages the same way they'll invalidate list/category caches — via the single shared `InvalidateCatalogueCacheAsync()` version bump, no separate invalidation path to maintain.

## Project Structure Notes

Backend: extends `Application/Catalogue/` and `Infrastructure/Catalogue/` (Story 3.1). Angular: new `features/catalogue/components/product-gallery/`, `pages/product-detail/`, `product-url.util.ts` — siblings of Story 3.2/3.3's existing catalogue components/pages. Flutter: new `features/catalogue/screens/product_detail_screen.dart`, `providers/product_detail_provider.dart` — siblings of Story 3.2/3.4's existing catalogue screens/providers.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 3.5 acceptance criteria (Epic 3 section)
- `_bmad-output/implementation-artifacts/3-1-api-catalogue-liste-et-filtres.md` — `ProductCatalogueService`/caching foundation this story extends
- `_bmad-output/implementation-artifacts/3-3-composants-productcard-et-filterchipbar-angular-web.md` — origin of the placeholder `/produits/:id` link this story replaces with the real slug-based route
- `_bmad-output/implementation-artifacts/3-4-composants-productcard-et-filterchipbar-flutter-mobile.md` — origin of the placeholder `/produits/:id` Flutter route this story finally gives a real destination

## Dev Agent Record

### Context Reference

- No `AskUserQuestion` needed — the client-side-slug and Flutter-scope-reduction decisions are implementation-detail interpretations following patterns already established this session (avoid unverifiable schema migrations where a no-migration alternative fully satisfies the AC's actual contract; scope web-specific AC language to the web platform), not foundational technology forks.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors; `dotnet test` — Application.UnitTests 116/116 passing (111 pre-existing + 5 new for `GetProductByIdAsync`).
- Angular verified via `ng build` (SSR + prerender, no errors) and `ng test` (Edge-as-ChromeHeadless, 49/49 passing both before and after review fixes — no new spec files added, following this codebase's established precedent of relying on build/prerender + full regression pass rather than per-feature Angular spec files).
- Flutter: no SDK in this environment (confirmed all session) — entirely unverified by tooling.
- Re-verified after applying all code-review fixes (Task 11 above reflects the final, post-review run).

### Completion Notes List

- All 11 tasks implemented and verified against real tooling where tooling exists (backend, Angular); Flutter remains hand-written and unverified, consistent with this project's standing environment limitation.
- The URL's "product-slug" is generated entirely client-side (`{slugified-name}-{full-guid}`) rather than via a new `Product.Slug` database column — avoids a schema migration this environment can't verify against the real, unreachable SQL Server instance, and is unnecessary anyway since the backend's own contract is ID-based. `ProductSummaryDto` gained a `CategorySlug` field (small, additive) so `ProductCardComponent` can build the URL without a second round-trip.
- Two reviewers independently converged on the same substantive bug — `ProductDetailComponent` not reacting to route param changes on a reused component instance — which was fixed by adopting the paramMap-subscription pattern already established in Stories 3.2/3.3, rather than inventing a new approach.
- Flutter's product-detail screen is deliberately lighter than Angular's (no keyboard nav, no ARIA carousel role, ID-based route not slug-based) since those AC requirements are inherently web concepts with no native-app equivalent, and no separate Flutter-specific story exists for this feature (unlike Stories 3.3/3.4's explicit platform split) — documented as a scope boundary, not silently narrowed.
- This story finally gives Story 3.3's and Story 3.4's placeholder `/produits/:id` product-card links a real destination on both platforms.

### File List

**Backend:**
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductDetailDto.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductSummaryDto.cs` (modified — added `CategorySlug`)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductByIdQuery.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductByIdQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IProductCatalogueService.cs` (modified — added `GetProductByIdAsync`)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (modified — `GetProductByIdAsync`, `MapToDetail`, `CategorySlug` in `MapToSummary`)
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` (modified — `GET /{id:guid}`)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/ProductCatalogueServiceTests.cs` (modified — new `GetProductByIdAsync` tests, updated the one existing positional `ProductSummaryDto` test construction)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/catalogue/catalogue.store.ts` (modified — added `categorySlug` to `ProductSummary`)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/product-url.util.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/product-detail.store.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-gallery/product-gallery.component.ts` (new; review fixes — dedicated `mainImageStatus`, `activeIndex` clamping)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-gallery/product-gallery.component.html` (new; review fixes — main image's own load/error bindings, single-image dot-row guard)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-gallery/product-gallery.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.ts` (new; review fix — `paramMap` subscription instead of one-shot `snapshot` read)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.html` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card/product-card.component.ts` (modified — `buildProductUrl` instead of placeholder `/produits/:id`)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card/product-card.component.html` (modified)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — `catalogue/:categorySlug/:productSlug` route)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/catalogue/providers/product_detail_provider.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/features/catalogue/screens/product_detail_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (modified — `/produits/:id` route, import)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (3.5 status)
