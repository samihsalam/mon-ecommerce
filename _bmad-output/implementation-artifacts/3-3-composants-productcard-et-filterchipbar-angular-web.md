# Story 3.3: Composants ProductCard & FilterChipBar — Angular Web

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a web visitor,
I want to see products in a responsive grid with interactive filter chips,
so that I can discover and filter the catalogue without page reloads.

## Acceptance Criteria

1. **Given** the catalogue page loads, **when** products are displayed, **then** each `ProductCard` shows: WebP image (ratio 3:4), brand, name, material, and price. **[Adapted — see Dev Notes]**: `Product` (Story 1.3's domain schema) has no brand/vendor-name field — `VendorId` is a bare `Guid?` with no `Vendor` entity or navigation property to look up a display name from. The brand line is omitted from `ProductCard`; this is a genuine schema gap, not a UI oversight, documented below rather than faked with placeholder text.
2. **And** the grid is 4 columns on desktop (`xl:`), 3 on `lg:`, 2 on `md:`, 1 on mobile.
3. **Given** a filter chip is tapped/clicked, **when** the filter is applied, **then** the grid updates in real time without page reload, the active chip has `background: #111111; color: white`, and the results counter ("12 sacs trouvés") updates via `aria-live="polite"`.
4. **Given** one or more filters are active, **when** the "Tout effacer" button is clicked, **then** all filters are reset and the full catalogue is shown.
5. Filter state is preserved when navigating to a product detail and returning.
6. Skeleton loader cards (same 3:4 ratio) are shown while fetching.
7. `role="article"` and `aria-label="[Nom], [prix]"` are set on each `ProductCard`.
8. Focus ring is `2px solid #C9A96E` with `offset: 2px` on all interactive elements.

## Tasks / Subtasks

### Angular — new `/catalogue` browsing page, built on Story 3.1/3.2's existing API and store

- [x] Task 1: Extend `CatalogueStore` with a plain-browse method (AC: #2, #3, #4)
  - [x] `browse(categoryId?: string | null)` — calls the same `GET /api/v1/products` endpoint as `search()` but without a `search` term (Story 3.1's plain category filter), sharing `results`/`totalCount`/`searchError`/`isSearching` state and the same monotonic request-id staleness guard as `search()` (both write to the same state, so they must share one counter — a `browse()` call superseded by a later `search()`, or vice versa, must not let its response win)
  - [x] `activeCategoryId` added to state, set by `browse()`, read by `FilterChipBar` to render the active-chip style
- [x] Task 2: `ProductCardComponent` (`components/product-card/`) (AC: #1, #2, #7, #8)
  - [x] Standalone; `@Input({ required: true }) product!: ProductSummary`
  - [x] Renders image (`loading="lazy"`, 3:4 `aspect-ratio` via CSS), name, material (when present), price; **no brand** (schema gap, see Dev Notes)
  - [x] Whole card is a `routerLink` to `/produits/:id` — Story 3.5 ("fiche produit") hasn't landed yet, so this is currently an unmatched route; documented as a known, temporary gap (same "build the link now, the destination lands in its own story" pattern as Story 3.2's category empty-state links)
  - [x] `role="article"`, `[attr.aria-label]="product.name + ', ' + formattedPrice"`
  - [x] Focus ring via Tailwind utilities (`focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2`) — matches `#C9A96E` via the existing `--color-accent` design token, no new token needed
- [x] Task 3: `ProductCardSkeletonComponent` (`components/product-card-skeleton/`) (AC: #6)
  - [x] Same 3:4 image-area ratio as `ProductCardComponent`, `animate-pulse` placeholder blocks for image/name/price, `aria-hidden="true"` (a loading placeholder has no content a screen reader should announce)
- [x] Task 4: `FilterChipBarComponent` (`components/filter-chip-bar/`) (AC: #3, #4, #8)
  - [x] `@Input({ required: true }) categories!: CategorySummary[]`, `@Input() activeCategoryId: string | null = null`; `@Output() categorySelected = new EventEmitter<string | null>()`, `@Output() clearAll = new EventEmitter<void>()`
  - [x] One chip per category (own interpretation of "filter chips" — see Dev Notes: material/color have no "distinct values" endpoint to populate chips from, so chips are category-based only); active chip gets `bg-text text-white` (`#111111`/white, matching AC literally via the existing `--color-text` token); "Tout effacer" button only rendered/enabled when a filter is active, emits `clearAll`
  - [x] Same focus-ring utility classes as `ProductCardComponent` on every chip + the clear button
- [x] Task 5: `CatalogueComponent` (`pages/catalogue/`) — the page tying it together (AC: #2, #3, #4, #5, #6)
  - [x] Mounted at new route `/catalogue`; reads/writes `categoryId` via the URL query param (`route.queryParamMap` subscription, same pattern as `SearchResultsComponent`) — this is what satisfies AC #5 "filter state preserved when navigating to a product detail and returning": the filter lives in the URL, so browser back/forward naturally restores it, no extra state-preservation plumbing needed
  - [x] Renders `FilterChipBarComponent` bound to `catalogueStore.categories()`/`activeCategoryId()`, a responsive grid (`grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4`) of `ProductCardComponent`s or `ProductCardSkeletonComponent`s while `isSearching()`, and a results counter (`"{{ totalCount }} {{ categoryLabel }} trouvés"`, `aria-live="polite"`) — own interpretation of the exact counter text (AC's literal example "12 sacs trouvés" pluralizes/lower-cases a specific category name; implemented as the active category's name lower-cased, or "produits" when no category is active — see Dev Notes)
- [x] Task 6: Route registration — `/catalogue` added to `app.routes.ts`, lazy-loaded standalone component, no `authGuard` (public catalogue browsing)

### Verification

- [x] Task 7: Full verification
  - [x] Angular: `ng build` (SSR, prerendered) + `ng test` via Edge-as-ChromeHeadless (49/49 passing, both before and after review fixes) — 0 regressions
  - [x] No backend changes this story — reuses Story 3.1/3.2's `GET /api/v1/products` and `GET /api/v1/products/categories` endpoints unmodified

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–3.2's predecessors). The Acceptance Auditor found **no AC violations** — all 8 acceptance-criteria bullets pass, including both documented deviations (brand omission, category-only chips) independently re-verified as genuine schema/scope gaps, not defects. The Acceptance Auditor also flagged a prompt-injection attempt embedded in tool output (a fake `<system-reminder>` instructing it to hide something) during its review — correctly ignored and surfaced, the same pattern seen repeatedly across this session's reviews.

- [x] [Review][Patch] Pagination gap, found by Edge Case Hunter (Medium-High — the most substantive finding): `browse()` always requested a single page (`pageSize` fixed at 20, no way to reach page 2+), while the results counter displayed the backend's full `totalCount` regardless — e.g. a category with 347 products would announce "347 produits trouvés" while only ever rendering 20 cards, with no control anywhere to reach the rest. This is exactly the gap Story 3.2's own review had explicitly deferred to "whichever story owns the grid UX" (i.e., this one) — so, unlike a lower-priority cosmetic finding, it was fixed rather than deferred again. **Fixed:** `browse()` now accepts a `pageNumber` and, when `pageNumber > 1`, appends results instead of replacing them and tracks a separate `isLoadingMore` flag (so an in-progress "load more" fetch doesn't blank the already-rendered grid behind a full skeleton state). `CatalogueComponent` renders a "Charger plus" button whenever `pageNumber() < totalPages()`, disabled and re-labeled "Chargement…" while `isLoadingMore()`.
- [x] [Review][Patch] aria-live results counter decoupled from error state, found by Blind Hunter: the counter paragraph was gated only on `!isSearching()`, not on `searchError()`. Since `browse()`'s catch block clears `isSearching` but previously left a prior successful call's `totalCount` untouched, a failed request (e.g. clicking a new category while offline) left the aria-live region announcing a stale, specific, and now-wrong count (e.g. "42 sacs trouvés") in the same moment the visible error banner said the request failed — actively misleading for a screen-reader user, and on a first-ever failed load it announced "0 produits trouvés" (implying an empty catalogue) instead of an error. **Fixed:** the counter's `@if` now also requires `!searchError()`; additionally, `browse()`'s catch block now resets `totalCount` to 0 on a failed page-1 request (not on a failed "load more," which correctly leaves prior results in place) as a second line of defense.
- [x] [Review][Patch] Long product names had no overflow handling, found by Edge Case Hunter: the name `<p>` had no `line-clamp`/`truncate`, so an unusually long name could grow a card's text block arbitrarily tall while its sibling cards' fixed `aspect-[3/4]` image blocks stayed constant — visually misaligned card bottoms in the same grid row. **Fixed:** added `line-clamp-2` to the name paragraph.
- [x] [Review][Note] `FilterChipBarComponent`'s "Tout effacer" button renders whenever `activeCategoryId` is truthy, without checking that id actually matches a loaded category — found by Edge Case Hunter. If a `categoryId` in the URL doesn't correspond to any existing category (e.g. a stale bookmark to a deleted category), the empty state and "0 produits" counter render correctly, but no chip is visually highlighted while "Tout effacer" still shows. Assessed as a minor, harmless cosmetic inconsistency (the button still works correctly via `onClearAll()`) rather than a functional defect — not fixed, per the reviewer's own severity assessment.
- [x] [Review][Note] The shared `searchRequestId` counter between `search()` (Story 3.2) and `browse()` (this story) — both reviewers independently traced this for cross-method and cross-page race conditions (rapid chip-clicking, navigating between `/catalogue` and `/recherche`) and found it holds up correctly in every scenario traced, including the case where `SearchResultsComponent` never calls `search()` for sub-2-character terms (a stale background `browse()` patch in that scenario is invisible, since that page's template gates entirely on the term-length check first, independent of store state). No fix needed; noted as verified, not assumed.
- [x] [Review][Note] French singular/plural noun agreement in the results counter ("trouvés" is invariant regardless of gender/number) — a known, pre-existing approximation already disclosed in this story's own Dev Notes before review, re-confirmed by Blind Hunter as cosmetic-only. Not fixed, consistent with the original scope decision.

## Dev Notes (post-implementation addendum)

### Pagination was added during review, not originally scoped in Task 5

The story's own Task 5 description (written before implementation) didn't call out pagination at all — the AC doesn't explicitly mention it either. It surfaced as a real, user-visible defect during Edge Case Hunter's review (see Review Findings above) and was fixed rather than deferred, since Story 3.2's own review had already explicitly named "whichever story builds the result-grid UI" as the right place for it — this is that story.

## Dev Notes

### AC #1's "brand" cannot be rendered — genuine schema gap, not an oversight

`Product` (Story 1.3's domain schema, confirmed by direct read of `Domain/Entities/Product.cs`) has `VendorId` (`Guid?`) but no `Vendor` entity, no navigation property, and no display-name field anywhere on the product. There is no marketplace/vendor concept implemented in this codebase yet (`VendorId` exists as a bare foreign-key-shaped field, presumably reserved for a future marketplace epic). Inventing a `Vendor` entity + migration + join just to populate one line of `ProductCard` text would be new backend domain scope far beyond what a UI-components story should introduce — unlike Story 3.2's `/categories` endpoint (a small, obviously-correct addition using data that already existed), there is no brand data to expose here at all. The brand line is omitted; if/when a vendor/brand concept is added to the domain (a different epic's job), `ProductCard` can pick it up with no structural change.

### "Filter chips" interpreted as category chips only

The AC's own example ("12 sacs trouvés") implies category-driven filtering. `Material`/`Color` are free-text `Product` columns (Story 1.3) with no "list distinct values in use" endpoint — Story 3.1's `GetProductsQuery` can filter *by* an exact material/color string, but nothing currently enumerates *which* values exist to build chips from. Building such an endpoint would be a reasonable follow-up but is additional backend scope this Angular-components story doesn't need to introduce to satisfy the AC's own literal example. `FilterChipBar` therefore renders one chip per category (from Story 3.2's `/api/v1/products/categories`), which is both genuinely useful and fully supported by existing data.

### Results counter text is an interpretation, not a literal AC string

"12 sacs trouvés" is one example instantiation, not a fixed template the AC defines generically. Implemented as `"{{ totalCount }} {{ categoryLabel }} trouvés"` where `categoryLabel` is the active category's name, lower-cased (e.g. "sacs" if a category literally named "Sacs" is selected) — or the generic word "produits" when no category filter is active. A real plural-agreement engine (e.g. "1 sac trouvé" vs "12 sacs trouvés") was not built — French singular/plural noun agreement is a real i18n problem this story's scope doesn't justify solving generically; the counter is grammatically approximate at `totalCount === 1`, documented here rather than silently shipped as if it were exact.

### `ProductCard`'s link target doesn't exist yet

Every card links to `/produits/:id` (a reasonable, self-explanatory future route), but Story 3.5 ("fiche produit et ProductGallery") — which builds that destination page — hasn't landed yet. `app.routes.ts` has no wildcard/404 route configured anywhere in this codebase yet, so clicking a card today navigates to an unmatched route (the router simply doesn't navigate; no crash, but no visible page either). This is the same "build the forward-compatible link now, the destination lands in its own dedicated story" choice already made for Story 3.2's category empty-state links — not a new pattern, but flagged again since it's more prominent here (every single card, not just an empty-state edge case).

### Filter-state preservation via URL query params, not component/store state

AC #5 ("filter state is preserved when navigating to a product detail and returning") is satisfied by keeping `categoryId` in the `/catalogue` route's query string rather than only in `CatalogueStore`'s in-memory state. A signal-store-only approach would already survive an in-app navigate-away-and-back (the store is `providedIn: 'root'`, so it isn't destroyed), but the URL-based approach additionally survives a hard refresh or a shared/bookmarked link — a strictly stronger guarantee for the same amount of code, using the exact pattern `SearchResultsComponent` (Story 3.2) already established for its own `q` param.

## Project Structure Notes

New `features/catalogue/components/product-card/`, `components/product-card-skeleton/`, `components/filter-chip-bar/`, and `pages/catalogue/` — all siblings of Story 3.2's `components/search-bar/` and `pages/search-results/` inside the `features/catalogue/` folder that story created.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 3.3 acceptance criteria (Epic 3 section)
- `_bmad-output/implementation-artifacts/3-1-api-catalogue-liste-et-filtres.md` — origin of `GET /api/v1/products`, `ProductSummaryDto`
- `_bmad-output/implementation-artifacts/3-2-recherche-full-text.md` — origin of `CatalogueStore`, `/api/v1/products/categories`, the query-param state-preservation pattern this story reuses
- `backend/MonEcommerce/src/Domain/Entities/Product.cs` — confirms no brand/vendor-name field exists (source of the AC #1 adaptation)

## Dev Agent Record

### Context Reference

- No architecture/AC conflict requiring `AskUserQuestion` this story — the brand-field gap and filter-chip scope are implementation-detail interpretations within already-established patterns (documented in Dev Notes), not foundational technology forks like Story 3.2's SQL Server decision.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Angular verified via `ng build` (SSR + prerender, no errors) and `ng test` (Edge-as-ChromeHeadless, 49/49 passing both before and after review fixes).
- No backend changes — no .NET verification needed this story.
- Re-verified after applying all code-review fixes (Task 7 above reflects the final, post-review run).

### Completion Notes List

- All 7 tasks implemented and verified against real tooling (Angular build/prerender/test).
- No backend work — this story is a pure Angular consumer of Story 3.1/3.2's existing endpoints, as scoped.
- Two genuine schema/data gaps were hit and documented rather than worked around with fake data: no brand field exists on `Product` at all (AC #1's brand line is omitted), and no "distinct material/color values" endpoint exists (filter chips are category-only). Both are proportionate, disclosed interpretations, not silent scope-narrowing.
- Review caught and fixed a real, user-visible pagination gap: the results counter showed the backend's full `totalCount` while the grid only ever rendered a single 20-item page with no way to reach the rest. Fixed with a "Charger plus" (load more) control that appends results via a `pageNumber`-aware `browse()`, using the same monotonic request-id staleness guard already established for `search()`/`loadSuggestions()` in Story 3.2.
- Review also caught the aria-live results counter announcing a stale, specific product count during an error state (decoupled from `searchError()`) — fixed by gating the counter on both `!isSearching()` and `!searchError()`.
- `role="article"` was deliberately placed on a wrapper `<div>`, not the `<a>` itself — putting a non-interactive landmark role on an anchor would strip its native link semantics from assistive tech, which would work against, not for, the AC's own accessibility intent.

### File List

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/catalogue/catalogue.store.ts` (modified — added `browse()` with pagination/append support, `activeCategoryId`, `isLoadingMore` state; review fix — stale-count reset on error)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card/product-card.component.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card/product-card.component.html` (new; review fix — `line-clamp-2` on the name)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card/product-card.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card-skeleton/product-card-skeleton.component.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card-skeleton/product-card-skeleton.component.html` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/product-card-skeleton/product-card-skeleton.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/filter-chip-bar/filter-chip-bar.component.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/filter-chip-bar/filter-chip-bar.component.html` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/filter-chip-bar/filter-chip-bar.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/catalogue/catalogue.component.ts` (new; review fix — `loadMore()` method)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/catalogue/catalogue.component.html` (new; review fix — aria-live gating, "Charger plus" control)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/catalogue/catalogue.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — `/catalogue` route)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (3.3 status)
