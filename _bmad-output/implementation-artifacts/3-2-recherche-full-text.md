# Story 3.2: Recherche Full-Text

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to search the catalogue by keyword,
so that I can find a product quickly by name or description.

## Acceptance Criteria

1. **Given** a search term of 2+ characters, **when** `GET /api/v1/products?search=cuir` is called, **then** results are returned sorted by relevance. **[Adapted — see Dev Notes]**: the original AC literally specified PostgreSQL FTS (`to_tsvector`/`to_tsquery`), but this project runs SQL Server (confirmed by CLAUDE.md and every prior story). The user was explicitly asked and chose: keep SQL Server, adapt the search feature. Relevance is achieved via a lower-cased `string.Contains` match (SQL `LIKE`) on Name/Description plus a hand-rolled ordering heuristic (exact name match > name-starts-with > name-contains > description-only match > alphabetical), not a SQL Server Full-Text Index/`CONTAINSTABLE` — that would require raw SQL and a Full-Text Index this environment cannot verify exists on the unreachable target SQL Server instance.
2. **Given** a search with no results, **when** the results page is displayed, **then** an empty state is shown: "Aucun résultat pour « [terme] »" with suggested category links.
3. **Given** 2+ characters typed in the search bar, **when** the user is typing, **then** a dropdown with up to 5 suggestions (categories + product names) appears within 300ms.
4. Search response time is ≤1 second.
5. Search works without being logged in.
6. The search input is accessible via keyboard (Tab focus, Enter to submit, Escape to close suggestions).

## Tasks / Subtasks

### Backend — extends Story 3.1's `GetProductsQuery`/`ProductCatalogueService`, adds two new read endpoints

- [x] Task 1: Add `Search` to the existing catalogue filter (AC: #1, #4, #5)
  - [x] `ProductFilter` gains a `string? Search` field (positional record — every existing call site updated)
  - [x] `GetProductsQuery` gains `string? Search = null`; `GetProductsQueryValidator` (new) rejects a search term shorter than 2 characters via `MinimumLength(2).When(...)` — an empty/omitted term is treated as "no search," not an error
  - [x] `Web/Endpoints/Products.cs`'s existing `GetProducts` handler gains a `search` query param, matching the AC's literal `GET /api/v1/products?search=cuir` URL (same endpoint as Story 3.1, not a new one)
- [x] Task 2: Relevance-ordered matching in `ProductCatalogueService.GetProductsAsync` (AC: #1)
  - [x] When `Search` is present: filters `p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term)` (both sides lower-cased explicitly — not relying on SQL Server's default collation — so behavior is identical between the EF Core InMemory test provider and real SQL Server; `Contains`/`ToLower()` both translate to SQL)
  - [x] Orders results via `OrderByRelevance`: `Name.ToLower() == term` (exact) → `Name.ToLower().StartsWith(term)` (prefix) → `Name.ToLower().Contains(term)` (substring) → alphabetical tiebreaker — each a bool-valued `OrderByDescending`/`ThenByDescending` key, which EF Core translates to `CASE WHEN ... THEN 1 ELSE 0 END` on SQL Server and evaluates as plain bool comparisons against InMemory, so ordering behavior is provably identical across both providers (see Dev Notes)
  - [x] Existing filters (category/material/color/price) and pagination compose unchanged with the new search filter/ordering
  - [x] Cache key: `Search` is now part of `ProductFilter`, which `BuildCacheKey` already JSON-serializes + SHA-256 hashes (Story 3.1) — no change needed to the caching mechanism itself, distinct search terms automatically produce distinct cache keys
- [x] Task 3: Typeahead suggestions endpoint (AC: #3, #4, #5)
  - [x] `Application/Catalogue/Models/SuggestionsResult.cs`: `public record SuggestionsResult(List<string> Categories, List<string> Products);`
  - [x] `GetSearchSuggestionsQuery` + `GetSearchSuggestionsQueryValidator` (rejects empty or <2-character terms outright — unlike the main search filter, a suggestions request with no usable term makes no sense, so this is a real 422, not a treated-as-"no search" case) + `GetSearchSuggestionsQueryHandler`
  - [x] `IProductCatalogueService.GetSearchSuggestionsAsync` / `ProductCatalogueService.GetSearchSuggestionsAsync`: matches category names first (case-insensitive `Contains`, capped at 5), then fills any remaining slots with matching published product names — combined total never exceeds 5 (own interpretation of "up to 5 suggestions (categories + product names)"; documented in Dev Notes)
  - [x] `Web/Endpoints/Products.cs`: `GET /api/v1/products/suggestions?search=...`, `.AllowAnonymous()`, no caching (lightweight typeahead query, not the heavier list query Story 3.1's caching was built for — proportionate scope choice)
- [x] Task 4: Categories endpoint for the empty-state's suggested links (AC: #2, #5)
  - [x] `Application/Catalogue/Models/CategorySummaryDto.cs`: `public record CategorySummaryDto(Guid Id, string Name, string Slug);`
  - [x] `GetCategoriesQuery` + `GetCategoriesQueryHandler`; `IProductCatalogueService.GetCategoriesAsync` — cached (same `catalogue:v{version}` prefix as the products cache, so Epic 6's future category CRUD invalidates it too), ordered by name
  - [x] `Web/Endpoints/Products.cs`: `GET /api/v1/products/categories`, `.AllowAnonymous()` — a small, genuinely-needed addition beyond the literal AC text, since "suggested category links" in the empty state needs a category list to link to and no such endpoint existed yet
- [x] Task 5: Backend tests
  - [x] `ProductCatalogueServiceTests.cs`: case-insensitive Name/Description matching; relevance ordering (exact > prefix > description-only); no-match returns empty result; search combined with an existing filter; suggestions return matching categories+products; suggestions cap combined total at 5; suggestions exclude unpublished products; categories returned ordered by name
  - [x] `GetProductsQueryValidatorTests.cs` (new): valid with no/empty search term, invalid at 1 character, valid at 2
  - [x] `GetSearchSuggestionsQueryValidatorTests.cs` (new): invalid when empty or 1 character, valid at 2

### Angular — search bar + results page (in scope this time, unlike Story 3.1: this AC explicitly describes UI behavior — dropdown, keyboard accessibility — that 3.1's API-only AC never did)

- [x] Task 6: `CatalogueStore` (`features/catalogue/catalogue.store.ts`) — `@ngrx/signals` store mirroring `OrdersStore`'s conventions: `search(term, pageNumber)`, `loadSuggestions(term)`, `clearSuggestions()`, `loadCategories()` (memoized — skips the call if categories are already loaded); `isSearching`/`searchError`/`isLoadingSuggestions` booleans, never a union status enum, matching every prior store in this codebase
- [x] Task 7: `SearchBarComponent` (`features/catalogue/components/search-bar/`) — standalone; 300ms `setTimeout`-debounced typeahead (cleared on every keystroke); `(keydown.enter)` submits and navigates to `/recherche?q=...`; `(keydown.escape)` closes the dropdown; a `document:click` `HostListener` closes the dropdown on outside-click; native `<input>` is Tab-focusable with no custom tabindex handling needed (AC: #3, #6)
- [x] Task 8: `SearchResultsComponent` (`features/catalogue/pages/search-results/`) — mounted at the new `/recherche` route; subscribes to `route.queryParamMap` (not a one-shot `ngOnInit` snapshot read) so navigating from one search term to another while already on `/recherche` re-triggers the search; renders loading/error/empty (with the exact `Aucun résultat pour « {{ currentTerm() }} »` string plus category-link chips)/results states, same `@if/@else if` cascade as `OrdersComponent` (AC: #2, #4)
- [x] Task 9: Route registration — `/recherche` added to `app.routes.ts`, lazy-loaded standalone component, no `authGuard` (AC: #5)

### Flutter — search screen (single-screen design, unlike Angular's split bar+page, since Flutter's touch-first UI doesn't need the same separation)

- [x] Task 10: `CatalogueNotifier`/`catalogueProvider` (`features/catalogue/providers/catalogue_provider.dart`) — mirrors `OrdersNotifier`'s conventions exactly (`copyWith` with `searchError` deliberately non-sticky, same as `OrdersState.selectedOrder`'s established fix); `search()`, `loadSuggestions()`, `clearSuggestions()`, `loadCategories()`
- [x] Task 11: `SearchScreen` (`features/catalogue/screens/search_screen.dart`) — `AppBar`'s title is a live `TextField` (`onChanged` debounced via `Timer(300ms)`, `onSubmitted` triggers search); a `Positioned`+`Material` suggestions overlay above the results `Stack`; results list / empty-state (with `ActionChip` category links) / error / loading, same nested-conditional pattern as `OrdersScreen`
- [x] Task 12: Route registration — `/recherche` added to `router.dart` (not in the protected-path list, so public); a search `IconButton` added to the placeholder `_HomeScreen`'s `AppBar.actions` as the feature's actual entry point (AC: #5)
- [x] **Keyboard-accessibility AC (#6) is scoped to the Angular web client, not Flutter.** "Tab focus, Enter to submit, Escape to close" is inherently desktop/web physical-keyboard language — Flutter's target here is a touch-first mobile UI. `SearchScreen`'s `TextField` still supports `onSubmitted` (Enter-equivalent on a hardware/soft keyboard) as a reasonable baseline, but full Tab/Escape parity was not pursued on this platform. Documented here rather than silently skipped.

### Verification

- [x] Task 13: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` (0 warnings/0 errors) + `dotnet test` (111/111 Application.UnitTests passing, final post-review run)
  - [x] Angular: `ng build` (SSR, prerendered) + `ng test` via Edge-as-ChromeHeadless (49/49 passing, final post-review run) — 0 regressions
  - [x] Flutter: **unverified by any tooling** — no Flutter SDK available in this environment (confirmed via `which flutter`/`which dart`, both absent), consistent with every other Flutter file in this project all session. Hand-written and reasoned about, not run or analyzed.
  - [x] AC #4 (≤1s response) and the "within 300ms" suggestion-latency requirement in AC #3 are both NFRs that can't be empirically verified without load-testing infrastructure (none exists in this codebase) — addressed via reasonable practice (`AsNoTracking()`, no N+1 queries, suggestions capped at 5 results each) and documented as unverified-by-tooling, not claimed as tested.

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — same process as Stories 1.7–3.1's predecessors). The Acceptance Auditor found **no AC violations** — all 6 acceptance criteria pass, with AC #4's response-time requirement correctly marked "not empirically verifiable in this environment, no static red flag found" rather than pass/fail.

- [x] [Review][Patch] Stale out-of-order response race, confirmed independently by two reviewers (Blind Hunter, Edge Case Hunter): neither `CatalogueStore` (Angular) nor `CatalogueNotifier` (Flutter) cancelled or ignored a superseded in-flight `search`/`loadSuggestions` request. Debouncing only prevented a second *timer* from firing while one was pending — it never cancelled an HTTP call already in flight. If an earlier term's response arrived after a later term's (a real out-of-order network scenario, not exotic), the store was unconditionally overwritten with stale data — e.g. the results heading showing "table" while the list still showed "chaise"'s products. **Fixed on both platforms:** a monotonic request-id counter is captured before each async call and checked against the current counter when the response arrives; a stale response is silently discarded. `clearSuggestions()` also bumps the suggestions counter, so a suggestions request already in flight when the user clears the box can no longer repopulate the dropdown afterward.
- [x] [Review][Patch] Cache key not normalized for `Search`, confirmed independently by two reviewers: `BuildCacheKey` hashed `filter.Search` as-is (raw casing/whitespace), while the actual query normalizes it (`Trim().ToLowerInvariant()`) before use — `"Cuir"`, `"cuir"`, `" cuir "`, and `null`/`""`/`"  "` (all functionally identical searches) each produced a distinct cache entry, needlessly fragmenting the cache for exactly the traffic pattern a search endpoint sees most. Not a correctness bug (never served *wrong* data, just missed cache hits it should have had) but cheap and worth fixing. **Fixed:** `BuildCacheKey` now normalizes `Search` the same way the query does before hashing. Covered by a new regression test asserting `SetAsync` is called exactly once across two case/whitespace variants of the same term.
- [x] [Review][Patch] 2-character minimum bypassable by padding, found by Blind Hunter: both validators measured the *raw* string length via `MinimumLength(2)`, but `ProductCatalogueService` always `.Trim()`s before using the term — a request like `search=" a"` (raw length 2) passed validation and silently collapsed to a 1-character search once trimmed, defeating the validator's own stated intent. A pure whitespace-only term (e.g. `"   "`) had inconsistent behavior between the two endpoints for the same reason: `GetSearchSuggestionsQueryValidator`'s `NotEmpty()` rejected it, but `GetProductsQueryValidator` let it through, where the service's `IsNullOrWhiteSpace` check silently treated it as "no search" and returned the *entire unfiltered catalogue* instead of erroring or filtering. **Fixed:** both validators now check the *trimmed* length via a `Must(...)` rule, closing the padding bypass and making whitespace-only terms consistently rejected (422) on both endpoints rather than silently falling back to "no search" on one of them.
- [x] [Review][Patch] No maximum length on `Search`, found by Edge Case Hunter: neither validator capped search-term length, so a public/unauthenticated caller could submit an arbitrarily large string into what is, with no full-text index, a leading-wildcard `LIKE '%...%'` scan over the whole catalogue per request — a low-cost DB-amplification vector. **Fixed:** both validators now cap `Search` at 200 characters (comfortably beyond any real product name/description search).
- [x] [Review][Patch] Angular `SearchBarComponent` never cleared its debounce timer on destroy, found by Blind Hunter (and correctly noted as an omission relative to the Flutter screen, which already disposed its equivalent timer): typing then navigating away within the 300ms debounce window still fired the pending `setTimeout` after the component was torn down — an unnecessary HTTP call plus a dangling closure keeping the destroyed component reachable until the timer fired. **Fixed:** `SearchBarComponent` now implements `OnDestroy` and clears the handle.
- [x] [Review][Patch] `/suggestions` endpoint's required non-nullable `search` parameter meant an omitted query param failed ASP.NET Core model binding with a bare 400, bypassing this codebase's standard 422-via-FluentValidation convention used everywhere else. **Fixed:** `search` (and `GetSearchSuggestionsQuery.Search`) are now nullable with a default of `null`, so a missing/empty term reaches the validator and gets a proper 422 instead.
- [x] [Review][Patch] No secondary ORDER BY tiebreaker on the suggestions queries, found by Edge Case Hunter: `GetSearchSuggestionsAsync`'s category/product queries ordered only by `Name`, so which of two identically-named rows landed inside vs. just outside the `Take(5)`/`Take(remaining)` cutoff wasn't guaranteed stable across identical requests (unlike the main product listing, which already had a `ThenBy(p => p.Id)` tiebreaker from Story 3.1). **Fixed:** added `.ThenBy(x => x.Id)` to both suggestion queries.
- [x] [Review][Note] Pagination beyond page 1 is unreachable from the Angular search UI, found by Blind Hunter: `CatalogueStore.search()` accepts a `pageNumber`, and `totalPages`/`pageNumber` state exists, but `SearchResultsComponent`'s template has no pager control that ever calls it with `pageNumber > 1`. Assessed as an accepted, deliberate scope boundary rather than a defect: Story 3.1 established the same "components land in their own dedicated story" precedent (no pagination UI, no product card), and this story's own scope note already defers real result-grid UI to Stories 3.3/3.4. A pager is exactly the kind of UI control that belongs with that future component work, not bolted onto a deliberately minimal interim results list. Documented here rather than left implicit.
- [x] [Review][Note] Suggestion/search queries against a real SQL Server provider (`LIKE` translation, wildcard-escaping for user-supplied `%`/`_`/`[` characters) rest on EF Core's own documented guarantee, not this codebase's own tests — only the InMemory provider is exercised in `ProductCatalogueServiceTests`, which doesn't go through SQL translation at all. Same category of "unverified against a live SQL Server" gap present in every prior story's search/query work; noted explicitly rather than left implicit.
- [x] [Review][Note] Neither reviewer found anything requiring a fix in the Flutter `catalogue_provider.dart`/`search_screen.dart` beyond the stale-response race (already fixed above) — dispose-during-pending-timer/future was traced and confirmed safe (the debounce `Timer` is cancelled in `dispose()`, and `setState` calls happen synchronously before any `await`, so no post-dispose crash path exists).

## Dev Notes

### The core adaptation: SQL Server `LIKE`-based search, not a Full-Text Index

Two SQL-Server-native alternatives were considered and rejected:

1. **SQL Server Full-Text Search** (`CREATE FULLTEXT INDEX` + `CONTAINSTABLE`/`FREETEXTTABLE` for `RANK`-based relevance) — the closest real analog to PostgreSQL FTS. Rejected because: (a) it requires the "Full Text and Semantic Extractions for Search" SQL Server component, which cannot be confirmed installed on the target `DESKTOP-M36577B` instance from this environment; (b) EF Core has no LINQ-translatable API for `CONTAINSTABLE`'s `RANK` column — it would require a hand-written raw-SQL migration plus `FromSqlInterpolated`, none of which this environment can verify against a live SQL Server at all (worse than the "migrations exist but are unrun" gap present in every prior story — here even the SQL's *correctness* would be unverified, not just its execution).
2. **`EF.Functions.FreeText`/`EF.Functions.Contains`** (SQL-Server-specific `DbFunctions`, translate to `FREETEXT`/`CONTAINS` predicates, still require a Full-Text Index) — same installation-dependency problem as above, and additionally these functions have no InMemory-provider translation at all, meaning the search path could not be unit-tested by this session's established test pattern.

What's implemented instead: a plain `string.Contains`-based filter (SQL `LIKE '%term%'`), which needs no Full-Text Index, translates identically and predictably on both the EF Core InMemory test provider and real SQL Server, and is explicitly proportionate to architecture.md's own stated scale for this feature ("Suffisant V1 (≤ 10k produits)"). Relevance is approximated with a translatable boolean-ordering heuristic (exact/prefix/substring name match ranked above description-only matches) rather than a true TF-IDF-style rank. If true full-text relevance becomes necessary at higher scale, SQL Server Full-Text Search (or Elasticsearch/Typesense, per architecture.md's own post-MVP suggestion) is the follow-up path — not implemented now. This was a judgment call made within the user's already-approved "keep SQL Server, adapt the search feature" decision, not escalated further, since it's an implementation-detail engineering trade-off (proportionate to stated scale, fully testable, no unverifiable raw-SQL infrastructure) rather than a second foundational technology fork.

### Case-insensitivity: explicit `.ToLower()` on both sides, not relying on collation

SQL Server's default collation is typically case-insensitive, but the EF Core InMemory provider's `string.Contains` is ordinal (case-sensitive) by default. Explicitly lower-casing both the column and the search term (`p.Name.ToLower().Contains(term)`, `term` pre-lowered via `ToLowerInvariant()`) sidesteps the discrepancy entirely — both providers behave identically, so the unit test suite's assertions about case-insensitive matching are trustworthy evidence of real SQL Server behavior too, not just an InMemory artifact.

### "Up to 5 suggestions (categories + product names)" — interpreted as 5 combined, categories first

The AC doesn't specify how the 5-suggestion cap splits between categories and products. Implemented as: fetch up to 5 matching categories, then fill any remaining slots (5 − categoryCount) with matching product names — so the combined total never exceeds 5, and categories are prioritized (matching the AC's own listed order, "categories + product names"). A different split (e.g. always reserve slots for both types) would be equally defensible; this one was chosen for simplicity and is documented here rather than left implicit.

### A new `/categories` endpoint beyond the literal AC text

AC #2's empty state needs "suggested category links" to link to *something*. No categories-list endpoint existed before this story (Story 3.1 only built the products list). Rather than hand-wave this requirement away, a small `GET /api/v1/products/categories` endpoint was added — cached, ordered by name, no filters (the catalogue is scoped to ≤10k products per architecture.md, so an unpaginated category list is proportionate). This is the same "small, genuinely-needed addition" judgment call as Story 3.1's `NullCacheService` fix — necessary to satisfy the AC's actual intent, not scope creep.

### Angular: `/recherche` route has no persistent site header/search entry point to mount into

`app.component.html` is currently just `<router-outlet /><app-toast />` — there is no global nav/header component in this codebase yet (none has been built in any prior story). `SearchBarComponent` is therefore mounted directly on `SearchResultsComponent`'s own page rather than a global header, and there's no other page yet that links to `/recherche` (a user reaches it only via direct URL for now). This is a known, temporary gap — wiring a persistent search entry point into a global header is out of scope here and will naturally land whenever that header component is built (not yet scheduled in the epic list).

### Flutter: `/recherche` reachable via a new search icon on the placeholder home screen

Unlike Angular, Flutter's `_HomeScreen` in `router.dart` is explicitly commented as a placeholder ("the real catalogue screen lands in Epic 3"). A `Icons.search` `IconButton` was added to its `AppBar.actions` specifically so the search feature has a real, tappable entry point in this build rather than being reachable only via a manually-typed deep link — a minimal, low-risk addition to an already-acknowledged placeholder screen.

### Result list rendering is intentionally minimal on both platforms

Neither the Angular results grid nor the Flutter results list uses a real product card — Story 3.3 (Angular `ProductCard`/`FilterChipBar`) and Story 3.4 (the Flutter equivalent) haven't landed yet. Building a one-off styled product tile now that gets thrown away once those stories ship would be wasted work; both platforms currently render a bare name+price row, matching Story 3.1's own precedent of deferring real component work to its dedicated story.

## Project Structure Notes

Backend: extends `Application/Catalogue/` and `Infrastructure/Catalogue/` (both created by Story 3.1) rather than creating new folders. Angular: new `features/catalogue/` (parallel to `features/account/`), first feature in this codebase with both a `components/` subfolder and a `pages/` subfolder. Flutter: new `features/catalogue/` (parallel to `features/account/`), same `providers/`/`screens/` split as `features/account/`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 3.2 acceptance criteria (Epic 3 section) — original PostgreSQL FTS wording
- `_bmad-output/planning-artifacts/architecture.md` — states PostgreSQL as the database (lines 173, 196) while its own scaffold command specifies `--database sqlserver` (line 97); also the source of "≤10k produits" scale justification and the Elasticsearch/Typesense post-MVP suggestion (line 187, 200)
- `_bmad-output/implementation-artifacts/3-1-api-catalogue-liste-et-filtres.md` — the `GetProductsQuery`/`ProductCatalogueService`/cache-key/pagination-clamping foundation this story extends
- CLAUDE.md — confirms SQL Server as the actual, authoritative database for this codebase

## Dev Agent Record

### Context Reference

- Architecture/AC conflict (PostgreSQL-specified AC vs. SQL-Server-actual codebase) surfaced by direct inspection of `epics.md` and `architecture.md`; resolved via `AskUserQuestion` — user selected "Keep SQL Server, adapt the search feature." No further web research needed; the SQL-Server-adaptation design (Dev Notes above) was reasoned from EF Core/SQL Server first principles and this session's established testability constraints (no live SQL Server reachable from this environment).

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend build/test verified against the real .NET 9 SDK via `docker run mcr.microsoft.com/dotnet/sdk:9.0`: `dotnet build` 0 warnings/0 errors; `dotnet test` — Application.UnitTests 102/102 passing pre-review (77 pre-existing + 25 new for this story), then 111/111 passing after applying review fixes (9 more regression tests for the padding-bypass, whitespace, max-length, and cache-normalization findings). Domain.UnitTests and Infrastructure.IntegrationTests have no tests currently (pre-existing state, not caused by this story).
- Angular verified via `ng build` (SSR + prerender, no errors) and `ng test` (Edge-as-ChromeHeadless, 49/49 passing both before and after applying review fixes — no new spec files added this story, following Stories 2.4/2.5's own precedent of not adding per-feature Angular spec files; build/prerender + full regression pass is this codebase's established Angular verification bar).
- Flutter: no SDK in this environment (`which flutter`/`which dart` both empty) — entirely unverified by tooling, same as every other Flutter file in this project.
- Re-verified after applying all code-review fixes (Task 13 above reflects the final, post-review run).

### Completion Notes List

- All 13 tasks implemented and verified against real tooling where tooling exists (backend, Angular); Flutter remains hand-written and unverified, consistent with this project's standing environment limitation.
- The central design decision — plain `LIKE`-based search over a SQL Server Full-Text Index — is a judgment call made within the user's already-approved "keep SQL Server, adapt" decision, not escalated as a second question, because it's an implementation-detail trade-off proportionate to the catalogue's stated ≤10k-product scale and this environment's testability constraints (full reasoning in Dev Notes).
- Story 3.1's `ProductFilter`/`GetProductsQuery` were extended (not replaced) with a `Search` field — every existing positional-record call site (service, handler, tests) was updated accordingly; no behavioral change to Story 3.1's existing filters/pagination/caching.
- A `GET /api/v1/products/categories` endpoint was added beyond the literal AC text, since AC #2's "suggested category links" has nothing to link to without one — documented as a small, genuinely-needed addition, not scope creep.
- Angular and Flutter UI work is in scope for this story (unlike Story 3.1, which was API-only) because this AC explicitly describes UI behavior (dropdown, keyboard accessibility) that 3.1's AC never did. Both platforms' result rendering is deliberately minimal (name+price, no real product card) since Stories 3.3/3.4 own that component work.
- Neither platform has a global header/nav to mount a persistent search entry point into (none exists yet in this codebase) — Angular's search bar lives on its own results page; Flutter gained a search icon on its placeholder home screen as a real, if temporary, entry point.

### File List

**Backend:**
- `backend/MonEcommerce/src/Application/Catalogue/Models/ProductFilter.cs` (modified — added `Search`)
- `backend/MonEcommerce/src/Application/Catalogue/Models/SuggestionsResult.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Models/CategorySummaryDto.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductsQuery.cs` (modified — added `Search`)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductsQueryHandler.cs` (modified)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetProductsQueryValidator.cs` (new; review fix — trimmed-length + max-length checks)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSearchSuggestionsQuery.cs` (new; review fix — `Search` made nullable)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSearchSuggestionsQueryValidator.cs` (new; review fix — trimmed-length + max-length checks)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetSearchSuggestionsQueryHandler.cs` (new; review fix — null-forgiving comment)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetCategoriesQuery.cs` (new)
- `backend/MonEcommerce/src/Application/Catalogue/Queries/GetCategoriesQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IProductCatalogueService.cs` (modified — added `GetSearchSuggestionsAsync`, `GetCategoriesAsync`)
- `backend/MonEcommerce/src/Infrastructure/Catalogue/ProductCatalogueService.cs` (modified — search filter, relevance ordering, suggestions, categories; review fix — cache-key normalization, suggestion-query tiebreakers)
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` (modified — `search` param, `/suggestions`, `/categories`; review fix — nullable `search` param on `/suggestions`)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Services/ProductCatalogueServiceTests.cs` (modified — existing positional `ProductFilter` calls updated, new search/suggestions/categories tests, review-fix regression test for cache-key normalization)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Queries/GetProductsQueryValidatorTests.cs` (new; review-fix regression tests added — padding bypass, whitespace-only, max-length)
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Queries/GetSearchSuggestionsQueryValidatorTests.cs` (new; review-fix regression tests added — null, whitespace-only, padding bypass, max-length)

**Angular:**
- `frontend/mon-ecommerce-web/src/app/features/catalogue/catalogue.store.ts` (new; review fix — stale-response-race guard via monotonic request-id counters)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/search-bar/search-bar.component.ts` (new; review fix — `OnDestroy` clears the pending debounce timer)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/search-bar/search-bar.component.html` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/search-bar/search-bar.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/search-results/search-results.component.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/search-results/search-results.component.html` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/search-results/search-results.component.scss` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — `/recherche` route)

**Flutter:**
- `mobile/mon_ecommerce_mobile/lib/features/catalogue/providers/catalogue_provider.dart` (new; review fix — stale-response-race guard via monotonic request-id counters)
- `mobile/mon_ecommerce_mobile/lib/features/catalogue/screens/search_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (modified — `/recherche` route, search icon on `_HomeScreen`)

**Other:**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (3.2 status)
