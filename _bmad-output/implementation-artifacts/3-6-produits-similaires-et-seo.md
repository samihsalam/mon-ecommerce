# Story 3.6: Produits Similaires & SEO

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to see similar products on the detail page and share a permanent link,
so that I can discover alternatives and easily return to a product.

## Acceptance Criteria

1. **Given** a product detail page, **when** the page renders, **then** a "Vous aimerez aussi" section shows up to 4 products from the same category.
2. **Given** Angular SSR renders the product page, **when** a search engine crawls it, **then** the page includes: dynamic `<title>` and `<meta description>`, Open Graph tags (`og:title`, `og:image`, `og:price`), and JSON-LD `Product` schema markup. **[Adapted — see Dev Notes]**: `og:price` isn't a real Open Graph property (Facebook's e-commerce OG spec uses `product:price:amount`/`product:price:currency`) — implemented using the real property names, preserving the AC's intent (price visible to crawlers/link previews) over its literal (non-standard) tag name.
3. **Given** the sitemap endpoint is called, **when** `GET /sitemap.xml` is requested, **then** all published product URLs are listed with `lastmod` dates.
4. Lighthouse SEO score is ≥90/100 on the product detail page. **[Unverifiable — see Dev Notes]**: no Lighthouse/browser-automation tooling exists in this environment.
5. Product URLs are permanent and human-readable (`/catalogue/sacs-cuir/tote-parisienne-marron`). **[Already satisfied by Story 3.5]** — no new work; the slug-based URL pattern this AC describes was built there.
6. The share button copies the canonical URL to clipboard with a toast confirmation.

## Tasks / Subtasks

### Backend — similar products, sitemap, shared slug helper

- [x] Task 1: `SlugHelper` (`Application/Common/Utilities/SlugHelper.cs`) (AC: #3, #5)
  - [x] Pure string utility mirroring the Angular `slugify()` (Story 3.5) exactly — lower-case, strip diacritics (`string.Normalize(NormalizationForm.FormD)` + filter `UnicodeCategory.NonSpacingMark`), replace non-alphanumerics with hyphens, trim — so the backend's sitemap URLs and the frontend's actual routed URLs are byte-for-byte identical for the same product name
- [x] Task 2: Similar products (AC: #1)
  - [x] `IProductCatalogueService.GetSimilarProductsAsync(Guid productId, ...)` / `ProductCatalogueService` implementation: looks up the product's own `CategoryId` (published only — an unpublished product has no "similar products" to surface), then queries other published products in that category, excluding the product itself, `OrderBy(Name).ThenBy(Id)`, `Take(4)`
  - [x] `GetSimilarProductsQuery(Guid ProductId) : IRequest<List<ProductSummaryDto>>` + handler; `GET /api/v1/products/{id:guid}/similar`, `.AllowAnonymous()`
  - [x] Cached the same versioned way as every other catalogue read (`catalogue:v{version}:similar:{id}`)
- [x] Task 3: Sitemap (AC: #3)
  - [x] `IProductCatalogueService.GetSitemapEntriesAsync(...)` → `List<SitemapEntryDto>` (`Url`, `LastModified`) — queries all published products + their category slugs, builds each URL via `SlugHelper` + `Frontend:BaseUrl` (existing config key, already used by `AuthService` for password-reset links), `LastModified` from `Product`'s own `BaseAuditableEntity.LastModified` audit field
  - [x] `Web/Endpoints/Sitemap.cs` — new `IEndpointGroup` with `RoutePrefix => ""` (sitemaps are conventionally served unprefixed at the site root, not under `/api/v1`), `GET /sitemap.xml`, builds the XML directly (`<urlset><url><loc>...</loc><lastmod>...</lastmod></url>...</urlset>`) from the service's DTOs, returns `Results.Content(xml, "application/xml")`
- [x] Task 4: Backend tests
  - [x] `SlugHelperTests.cs`: matches the Angular algorithm's known outputs (accented characters, multiple spaces, leading/trailing punctuation)
  - [x] `ProductCatalogueServiceTests.cs`: similar products excludes self, only same category, caps at 4, published-only (both the source product and the candidates), empty when the product has no category-mates; sitemap entries include only published products, correct URL shape, correct `lastmod`

### Angular — similar products section, SEO service, share button

- [x] Task 5: `ProductDetailStore` extended with `loadSimilarProducts(productId)` (AC: #1)
  - [x] New state: `similarProducts: ProductSummary[]`, reusing the SAME `ProductSummary` interface `CatalogueStore` already exports (identical shape, avoids a duplicate type)
- [x] Task 6: `SeoService` (`core/services/seo.service.ts`) (AC: #2)
  - [x] `setProductSeo(product, canonicalUrl)`: `Title.setTitle(...)`, `Meta.updateTag({name:'description', ...})`, `Meta.updateTag({property:'og:title'|'og:image'|'og:type'|'og:url', ...})`, `Meta.updateTag({property:'product:price:amount'|'product:price:currency', ...})` (the real OG e-commerce properties — see AC #2's adaptation note), a `<link rel="canonical">` tag, and a JSON-LD `<script type="application/ld+json">` `Product` schema (`name`, `description`, `image`, `offers: {@type: 'Offer', price, priceCurrency, availability}`) — injected/replaced via `DOCUMENT`/`Renderer2` so repeated client-side navigation between products doesn't pile up duplicate `<script>` tags
  - [x] Called from `ProductDetailComponent` once the product loads, both server- and client-side (Angular's `Title`/`Meta` services are SSR-safe by design — this is exactly why the AC ties this to "Angular SSR renders the page")
- [x] Task 7: "Vous aimerez aussi" section + share button (AC: #1, #6)
  - [x] `ProductDetailComponent`/`.html`: renders up to 4 `ProductCardComponent`s from `productDetailStore.similarProducts()` in a small grid, only when non-empty
  - [x] Share button: `navigator.clipboard.writeText(canonicalUrl)` (guarded — Clipboard API can be unavailable in some contexts) → `ToastService.show('Lien copié !')` on success, a distinct error toast on failure
- [x] Task 8: Angular tests — **closing a real gap discovered this story: Stories 3.2–3.5's catalogue stores/components shipped with NO spec files at all**, unlike the account/auth features (`orders.store.spec.ts`, `auth.store.spec.ts`, etc. — genuine, substantive `HttpTestingController`-based tests already established as this codebase's real convention). This was a miss, not a deliberate scope choice, corrected starting now
  - [x] `product-detail.store.spec.ts`: `loadProduct`/`loadSimilarProducts` success + error paths via `HttpTestingController`, matching `orders.store.spec.ts`'s exact pattern
  - [x] `seo.service.spec.ts`: verifies `Title`/`Meta` tags and the JSON-LD script tag are set correctly, and that calling `setProductSeo` twice replaces (not duplicates) the script tag

### Verification

- [x] Task 9: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] Angular: `ng build` (SSR, prerendered) + `ng test` via Edge-as-ChromeHeadless
  - [x] AC #4 (Lighthouse ≥90) unverifiable — no Lighthouse/browser-automation tooling in this environment; addressed via reasonable SEO practice (SSR-rendered meta tags, semantic URLs, JSON-LD) and documented as unverified-by-tooling, not claimed as tested

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–3.5's predecessors). The Acceptance Auditor found **no AC violations** — all 6 acceptance-criteria bullets pass, including every documented deviation (`og:price`→real OG properties, Lighthouse unverifiable, Story 3.5's URL scheme confirmed not regressed) independently re-verified as genuine and correctly wired end-to-end.

- [x] [Review][Patch] Missing/empty `Frontend:BaseUrl` could crash or silently corrupt the public sitemap endpoint, found by Edge Case Hunter (High): `GetSitemapEntriesAsync` used a bare `_configuration["Frontend:BaseUrl"]!.TrimEnd('/')` — if the config key were genuinely absent at runtime (no startup validation forces it to exist), this throws a bare `NullReferenceException` out of a public, unauthenticated, crawler-facing endpoint (an opaque 500); if the value were present but empty, `TrimEnd` is a no-op and produces scheme-less/host-less `<loc>` URLs that silently violate the sitemap protocol's fully-qualified-URL requirement without failing at all. Blind Hunter separately confirmed the exact same `!`-assertion pattern already exists at `AuthService.cs:116` for password-reset emails — not a new risk class introduced by this story, but `AuthService`'s usage is an authenticated-adjacent internal flow, while a sitemap is anonymous and crawler-facing, warranting a stricter posture specifically here. **Fixed:** explicit `string.IsNullOrWhiteSpace` check throwing a clear, descriptive `InvalidOperationException` instead of either failure mode. Covered by a new parameterized regression test (`null`, `""`, whitespace-only).
- [x] [Review][Patch] `loadSimilarProducts` had no staleness guard, confirmed independently by all three reviewers (the most consistently-flagged finding this story, and directly contradicted my own code comment claiming it was "self-correcting" — it demonstrably is not): rapid navigation from product A to product B (e.g. clicking a "Vous aimerez aussi" link this very story adds) fires `loadSimilarProducts(A)` then `loadSimilarProducts(B)`; if A's response arrives after B's (plausible under ordinary network jitter, independent of request order), it unconditionally overwrites `similarProducts` with A's stale list while the rest of the page still shows product B — a real, reachable, non-self-correcting cross-product content mismatch. **Fixed:** added a dedicated monotonic request-id counter for `loadSimilarProducts` (separate from `loadProduct`'s own counter, since the two HTTP calls resolve independently), discarding any response that arrives after a newer call has superseded it — the same pattern already established for `search()`/`browse()` in `CatalogueStore` (Stories 3.2/3.3).
- [x] [Review][Patch] Empty `og:image` was actively worse than no tag at all, found by Edge Case Hunter (Medium): `product.imageUrls[0] ?? ''` meant a product with no images got `<meta property="og:image" content="">` — an empty string is not "no image" per the Open Graph spec (which requires a valid absolute URL), so link-preview generators render a broken-image icon rather than gracefully omitting the preview image. Additionally, since `SeoService`'s tags persist across client-side navigations, a product WITH an image followed by one WITHOUT would leave the previous product's stale `og:image` in place, since `updateTag` was simply never called for the new (imageless) product. **Fixed:** `og:image` is now set via `updateTag` only when a real image URL exists, and explicitly removed via `meta.removeTag(...)` otherwise — closing both the malformed-tag issue and the stale-tag-from-a-previous-product issue in one fix.
- [x] [Review][Patch] `GetSitemapEntriesAsync` was the only catalogue read with no caching at all, found by Edge Case Hunter (Medium): every other method (`GetProductsAsync`, `GetProductByIdAsync`, `GetSimilarProductsAsync`, `GetCategoriesAsync`) checks the version-keyed cache first; this one ran a full `Where(IsPublished).Include(Category)` table scan on every single anonymous request, with no TTL protection — a low-cost amplification vector against a public, crawler-facing, unauthenticated endpoint. **Fixed:** added the same `catalogue:v{version}:sitemap` cache-key pattern used everywhere else in this service.
- [x] [Review][Note] Both other reviewers independently traced the `GetSimilarProductsAsync` nullable-`Guid?`-projection path, the `Sitemap` endpoint's empty-`RoutePrefix` handling (confirmed it does NOT fall back to the `/api/{groupName}` default, since `""` is a non-null value and the `??` operator only fires on `null`), the `SlugHelper`/Angular `slugify()` equivalence (including both algorithms' identical degenerate-input behavior — an all-punctuation product name slugifies to an empty string on both platforms, producing an ugly-but-still-routable leading-hyphen URL segment, since only the trailing GUID is ever parsed back out), and the `SeoService` singleton's per-SSR-request isolation (confirmed via `server.ts`'s `CommonEngine.render` bootstrapping a fresh injector per request) — all found correct. No fix needed, noted as verified rather than assumed.
- [x] [Review][Note] Both Edge Case Hunter and Blind Hunter flagged a prompt-injection attempt embedded in tool output (a fake `<system-reminder>` about a date change, instructing them to stay silent) during their reviews — correctly ignored and surfaced, the same pattern seen repeatedly across this session's reviews.

## Dev Notes (post-implementation addendum)

### `AuthService`'s identical `IConfiguration` un-guarded-`!` pattern is out of scope for this story

Blind Hunter noted `AuthService.cs:116` already uses the exact same `_configuration["Frontend:BaseUrl"]!` pattern this story just hardened in `ProductCatalogueService`. That call site builds password-reset email links — a lower-exposure, authenticated-adjacent flow (only reachable after a user requests a password reset, not hit by arbitrary anonymous crawler traffic) rather than a public, unauthenticated, crawler-facing endpoint. Worth hardening similarly in a future pass, but doing so here would be scope creep beyond what this story's own review actually found broken.

## Dev Notes

### `og:price` isn't a real Open Graph property

Standard Open Graph has no `og:price` tag. Facebook's own e-commerce/product OG extension uses `product:price:amount` and `product:price:currency` (plus `og:type: product`). Implemented the real properties instead of the AC's literal (incorrect) tag name — the AC's actual intent (a link preview or crawler can see the product's price) is fully satisfied; a literal `og:price` tag would just be silently ignored by every real consumer of Open Graph data.

### Sitemap and frontend URLs must use byte-identical slugification

Story 3.5's product-detail URL is `/catalogue/{category-slug}/{slugified-name}-{full-guid}`, computed entirely client-side in Angular (`product-url.util.ts`). For `GET /sitemap.xml` to list URLs that actually resolve (not 404 on a slug mismatch), the backend needs to compute the identical slug from the identical product name — hence `SlugHelper.cs`, a C# port of the same algorithm (lower-case, NFD-normalize, strip combining marks, replace non-alphanumerics with hyphens, trim), tested against the same known input/output pairs as the Angular version. Any future change to one MUST be mirrored in the other, or sitemap URLs silently stop resolving — noted here since nothing enforces this consistency automatically (no shared code between the two runtimes).

### A real gap found and fixed: Stories 3.2–3.5 shipped without Angular spec files

While building this story's Angular tests, direct inspection of `features/account/`/`features/auth/` confirmed genuine, substantive `.spec.ts` files already exist for those features' stores and components (e.g. `orders.store.spec.ts`, `auth.store.spec.ts`) — real `HttpTestingController`-based tests, not scaffolding stubs. Every catalogue story this session (3.2 through 3.5) shipped its stores/components with **no spec files at all**, incorrectly documented in those stories' own Dev Agent Records as "following this codebase's own precedent of not adding per-feature Angular spec files" — that precedent doesn't actually exist; it was a mistaken inference from an incomplete look at the codebase early in Epic 3. This story's own new Angular code (Task 8) is properly tested. A follow-up pass retroactively adding spec coverage for the untested catalogue stores/components from Stories 3.2–3.5 is warranted and will be pursued as a distinct, explicitly-flagged follow-up rather than silently bundled into this story's diff.

## Project Structure Notes

Backend: extends `Application/Catalogue/`, `Infrastructure/Catalogue/` (Story 3.1); new `Application/Common/Utilities/SlugHelper.cs`; new `Web/Endpoints/Sitemap.cs` (root-level `IEndpointGroup`, parallel to `Products.cs`/`Auth.cs`/`Account.cs` but with an empty `RoutePrefix`). Angular: new `core/services/seo.service.ts` (parallel to `core/services/toast.service.ts`); extends `product-detail.store.ts`, `product-detail.component.ts` (Story 3.5).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 3.6 acceptance criteria (Epic 3 section)
- `_bmad-output/implementation-artifacts/3-5-fiche-produit-et-productgallery.md` — origin of the client-side slug/URL pattern this story's sitemap must mirror, and of `ProductDetailStore`/`ProductDetailComponent`
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` — origin of the `Frontend:BaseUrl` config key reused here for building absolute sitemap URLs

## Dev Agent Record

### Context Reference

- No `AskUserQuestion` needed — the `og:price`→real-OG-property mapping is the same class of "adapt literal AC wording to the real, correct mechanism" judgment call made throughout this session (e.g. Story 3.2's SQL Server search, Story 3.4's "Material CDK"→`showModalBottomSheet`).

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors; `dotnet test` — Application.UnitTests 130/130 passing (final post-review run; 126 after initial implementation, +4 regression tests added during review: missing-BaseUrl ×3 parameterized cases counted as 3, plus the 2-siblings-under-cap case).
- Angular verified via `ng build` (SSR + prerender, no errors) and `ng test` (Edge-as-ChromeHeadless, 61/61 passing both before and after review fixes — 12 new spec tests added this story across `product-detail.store.spec.ts` and `seo.service.spec.ts`, closing the real gap discovered this story: Stories 3.2–3.5's catalogue Angular code shipped with no spec files at all).
- No Flutter changes this story (Story 3.6's AC is entirely backend/Angular — SEO/sitemap/canonical-URL concepts have no Flutter-native-app equivalent, consistent with Story 3.5's own scope reasoning for web-specific ACs).
- Re-verified after applying all code-review fixes (Task 9 above reflects the final, post-review run).

### Completion Notes List

- All 9 tasks implemented and verified against real tooling (backend, Angular).
- `og:price` was adapted to the real Open Graph e-commerce properties (`product:price:amount`/`product:price:currency`) since `og:price` itself isn't a real, standardized tag — documented as a literal-AC-wording adaptation, not a scope reduction.
- `SlugHelper.cs` is a hand-ported C# mirror of Angular's `slugify()` (Story 3.5) — both algorithms were cross-checked against the same known input/output pairs (accented characters, multiple spaces, punctuation) to keep sitemap URLs and actual routed URLs byte-for-byte identical.
- **A real, previously-undiscovered gap was found and corrected this story**: Stories 3.2 through 3.5 shipped their Angular catalogue stores/components with zero spec test files, incorrectly justified at the time as "this codebase's own precedent" — direct inspection of the account/auth features (predating this session's visible history) confirmed substantive `.spec.ts` files already exist there. This story's own new Angular code is properly tested; a follow-up pass to retroactively backfill spec coverage for Stories 3.2–3.5 is warranted and flagged separately, not silently bundled into this diff.
- 3-layer review caught and fixed four real issues: a crash/silent-corruption risk in the sitemap endpoint on missing configuration, a genuinely reachable stale-data race in "similar products" during rapid product-to-product navigation (independently found by all three reviewers), a malformed/stale `og:image` tag for imageless products, and a missing cache layer on the one previously-uncached catalogue read path.

### File List

**Backend:**
- `backend/MonEcommerce/src/Application/Common/Utilities/SlugHelper.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Models/SitemapEntryDto.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSimilarProductsQuery.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSimilarProductsQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSitemapEntriesQuery.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSitemapEntriesQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IProductCatalogueService.cs` (modified — added `GetSimilarProductsAsync`, `GetSitemapEntriesAsync`)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (modified — new `IConfiguration` dependency, `GetSimilarProductsAsync`, `GetSitemapEntriesAsync`; review fixes — explicit `Frontend:BaseUrl` guard, sitemap caching)
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` (modified — `GET /{id:guid}/similar`)
- `backend/MonEcommerce/src/Web/Endpoints/Sitemap.cs` (new — `GET /sitemap.xml`, empty `RoutePrefix`)
- `backend/MonEcommerce/tests/Application.UnitTests/Common/Utilities/SlugHelperTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/ProductCatalogueServiceTests.cs` (modified — new similar-products/sitemap tests, `IConfiguration` mock in `CreateService`; review fixes — missing-BaseUrl regression test, 2-siblings-under-cap test)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/core/services/seo.service.ts` (new; review fix — conditional `og:image` with stale-tag removal)
- `frontend/mon-ecommerce-web/src/app/core/services/seo.service.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/product-detail.store.ts` (modified — added `similarProducts` state, `loadSimilarProducts`; review fix — dedicated request-id staleness guard)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/product-detail.store.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.ts` (modified — SEO `effect()`, similar products loading, share button)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.html` (modified — "Vous aimerez aussi" section, share button)
- `frontend/mon-ecommerce-web/src/environments/environment.ts` (modified — added `siteUrl`)
- `frontend/mon-ecommerce-web/src/environments/environment.production.ts` (modified — added `siteUrl`)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (3.6 status, epic-3 done)
