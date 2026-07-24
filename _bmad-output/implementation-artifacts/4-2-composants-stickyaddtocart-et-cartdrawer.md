# Story 4.2: Composants StickyAddToCart & CartDrawer

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to add a product from the detail page and view my cart in a slide-in panel,
so that I can convert without losing my browsing context.

## Acceptance Criteria

1. **Given** a visitor is on a product detail page on mobile, **when** they scroll down, **then** the `StickyAddToCart` bar remains fixed at the bottom showing price and "Ajouter au panier" button.
2. **Given** the "Ajouter au panier" button is tapped, **when** the item is added successfully, **then** the cart badge increments with a subtle animation and a snackbar confirms ("`{ProductName}` ajoutée au panier", 3s auto-dismiss). `aria-live="polite"` announces the confirmation to screen readers.
3. **Given** the cart icon is tapped, **when** the `CartDrawer` opens, **then** it slides in from the right (desktop) or bottom (mobile) with focus trapped inside. Pressing Escape closes it and returns focus to the trigger element.
4. **Given** the cart is empty, **when** the `CartDrawer` opens, **then** an empty state is shown with an illustration and "Découvrir notre catalogue" CTA.
5. `role="dialog"` and `aria-modal="true"` are set on the `CartDrawer`; body scroll is locked while the drawer is open.
6. `StickyAddToCart` shows `aria-disabled` and "Rupture de stock" text when stock is 0.

## Tasks / Subtasks

### Prerequisite: no site-wide header exists yet (AC: #3)

- [x] Task 0: Minimal site header/toolbar
  - [x] **Scope note, not scope creep**: no header/nav component exists anywhere in this Angular app yet (`app.component.html` is just `<router-outlet /><app-toast />`) — there is no `<epic>/<story>` elsewhere in the backlog that owns one either (checked `epics.md` end to end). AC #3's "the cart icon is tapped" presupposes a persistent cart icon exists somewhere, so this story must create the minimal shell that hosts it — a full site nav (categories, search bar relocation, account menu, etc.) is explicitly OUT of scope; build only what AC #3 needs: a slim top bar with the site name/logo linking to `/` and a cart icon+badge on the right.
  - [x] New `core/components/header/header.component.ts` (standalone), mounted in `app.component.html` alongside the existing `<router-outlet />`/`<app-toast />` — same "always-on shell piece" pattern as `ToastComponent`
  - [x] Cart icon shows the current item count as a badge (0 → no badge shown, matching common cart-icon UX and avoiding a meaningless "0" badge on first load)

### Task 1: Dependencies

- [x] Install `@angular/cdk` (`^19.2.0`, matching the already-installed `@angular/core: ^19.2.0` — the project has no CDK dependency yet despite the UX spec calling for `FocusTrap`/`LiveAnnouncer`/`BreakpointObserver`; this story is the first to actually need it)
  - [x] `cdk/a11y`'s `cdkTrapFocus`/`cdkTrapFocusAutoCapture` directives satisfy AC #3's focus-trap requirement without hand-rolling tab-cycling logic

### Task 2: Cart identity — session header plumbing (AC: #2, #3, #4)

- [x] The backend (Story 4.1) resolves the cart by `X-Cart-Session-Id` request header for anonymous visitors, and returns a freshly-generated one via a **response** header when neither an authenticated user nor an existing session header was present. The Angular client owns generating/persisting this id going forward (this was explicitly deferred to this story in Story 4.1's Dev Notes).
  - [x] New `core/constants/storage-keys.ts` addition: `CART_SESSION_ID_KEY = 'cartSessionId'` (existing file already holds `ACCESS_TOKEN_KEY`/`REFRESH_TOKEN_KEY` — same convention)
  - [x] New `core/interceptors/cart-session.interceptor.ts` (parallel to the existing `auth.interceptor.ts`, same `isPlatformBrowser` SSR guard): attaches `X-Cart-Session-Id` from `localStorage` to every outgoing request when present (harmless no-op on non-cart endpoints; required on `POST /auth/login` too, since `Auth.Login`'s merge-on-login reads it from THAT request specifically — Story 4.1's `Auth.cs`). Also reads the response's `X-Cart-Session-Id` header (present whenever the server had to generate a fresh one) and persists it to `localStorage` if it differs from what's currently stored.
  - [x] Register the new interceptor in `app.config.ts` alongside `authInterceptor` (interceptor order doesn't matter here — they touch different headers)

### Task 3: `CartStore` (AC: #2, #3, #4)

- [x] New `features/cart/cart.store.ts` — same `signalStore({ providedIn: 'root' }, withState, withMethods)` shape as `AuthStore`/`ProductDetailStore`
  - [x] State: `{ items: CartItem[]; totalInCents: number; isOpen: boolean; isLoading: boolean; error: string | null }`
  - [x] `CartItem`/`CartDto` types mirror the backend DTOs exactly (`Application/Carts/Models/CartItemDto.cs`, `CartDto.cs`): `{ id, productId, productName, imageUrl, unitPriceInCents, quantity, lineTotalInCents }` / `{ items, totalInCents }`
  - [x] `loadCart()`: `GET /api/v1/cart`
  - [x] `addItem(productId, quantity)`: `POST /api/v1/cart/items`; on success, replaces `items`/`totalInCents` from the response (the endpoint already returns the full updated cart — no need for a separate re-fetch, same "response already has everything" pattern as `AuthStore.login`)
  - [x] `updateItemQuantity(itemId, quantity)`: `PATCH /api/v1/cart/items/{itemId}` (quantity 0 removes — same contract Story 4.1 already implemented server-side)
  - [x] `removeItem(itemId)`: `DELETE /api/v1/cart/items/{itemId}`
  - [x] `open()`/`close()`: toggle `isOpen` (drives `CartDrawerComponent`'s visibility — the component itself owns focus-trap/Escape/body-scroll-lock mechanics, the store just owns *whether* it should be open)
  - [x] `itemCount` computed signal (`withComputed`, matching this codebase's Signal Store conventions elsewhere): sum of `items[].quantity` — this is what the header badge (Task 0) reads
  - [x] Call `loadCart()` once at store construction time (mirrors `AuthStore`'s "hydrate from persisted state on construction" pattern) — SSR-guarded: only fire the HTTP call in the browser (`isPlatformBrowser`), same reasoning as `AuthStore`'s `localStorage` reads, since there's no meaningful anonymous cart to render during SSR anyway

### Task 4: `StickyAddToCartComponent` (AC: #1, #2, #6)

- [x] New `features/catalogue/components/sticky-add-to-cart/sticky-add-to-cart.component.ts`, mounted in `product-detail.component.html` (Story 3.5's page)
  - [x] Inputs: the current `ProductDetail` (price, stock, id, name) — this component is presentational/dumb; it calls `CartStore.addItem()` directly (same "component calls the store" pattern as every page component in this codebase, no extra facade layer)
  - [x] Fixed-bottom, full-width on mobile (`fixed bottom-0 inset-x-0`, Tailwind); inline in the detail page's right column on desktop (`--breakpoint-lg` token already in `styles.scss`) — matches the UX spec's documented adaptation ("fixe bas pleine largeur mobile → colonne droite inline desktop")
  - [x] `stockQuantity === 0` → button gets `aria-disabled="true"` (not the native `disabled` attribute — `aria-disabled` was specifically called out in the AC, and keeps the element focusable so screen reader users still land on it and hear why) and its label becomes "Rupture de stock" instead of "Ajouter au panier"
  - [x] On successful `addItem()`: call `ToastService.show(...)` with the exact copy pattern from the AC (`"${product.name} ajoutée au panier"`) — see Task 6 for the 3s-vs-4s default mismatch this surfaces
  - [x] Badge "subtle animation" (AC #2): a CSS transition/keyframe on the header's badge element (Task 0) triggered when `CartStore.itemCount()` changes — e.g. a brief scale pulse; kept to a plain CSS class toggle, no animation library needed

### Task 5: `CartDrawerComponent` (AC: #3, #4, #5)

- [x] New `core/components/cart-drawer/cart-drawer.component.ts`, mounted in `app.component.html` next to the header (Task 0) — a global, always-present overlay driven entirely by `CartStore.isOpen()`, not a route
  - [x] `role="dialog"`, `aria-modal="true"`, `aria-label` (e.g. "Panier") on the drawer root
  - [x] `cdkTrapFocus` (Task 1's CDK dependency) on the drawer's content container while `isOpen()` is true. **Implementation deviation**: `cdkTrapFocusAutoCapture` was deliberately NOT used — it captures/moves focus asynchronously on its own schedule, which could race against this component's own capture of "what was focused before opening" (risk of capturing an element already inside the drawer instead of the real external trigger). Focus capture/restore is instead handled manually and synchronously in one `effect()`: capture `document.activeElement` the instant `isOpen()` flips true (before anything else can steal it), defer moving focus onto the dialog root (`tabindex="-1"`, template ref) via `setTimeout` so it runs after the view renders, and restore the captured element's focus synchronously when `isOpen()` flips false.
  - [x] Escape key closes the drawer AND returns focus to the trigger element — handled by the same manual capture/restore `effect()` above, not a per-trigger reference passed in from `HeaderComponent`
  - [x] Body scroll lock while open: toggle a class on `document.body` (e.g. `overflow-hidden`) in an `effect()` reacting to `isOpen()`, cleaned up when it closes — SSR-safe (`isPlatformBrowser` guard, `document` doesn't meaningfully exist server-side)
  - [x] Slide-in from the right on desktop (`lg:` breakpoint, ~400px panel per the UX spec's "panel latéral 400px desktop"), from the bottom on mobile (full-width bottom sheet) — CSS transform transition, no CDK Overlay position strategy needed since this isn't anchored to a trigger element's position (unlike a typical CDK-overlay-positioned menu)
  - [x] Empty state (AC #4): shown when `CartStore.items().length === 0` — illustration (a simple inline SVG or an existing static asset; no new asset pipeline needed for a single icon) + "Découvrir notre catalogue" button linking to `/catalogue` (closes the drawer on click, same as any other in-drawer navigation)
  - [x] Non-empty state: list of cart items (image, name, unit price, quantity stepper wired to `updateItemQuantity`, remove button wired to `removeItem`), running total, and a placeholder/disabled "Passer commande" CTA — checkout itself is Story 4.3+'s scope, so this button can exist visually but need not navigate anywhere functional yet (avoid a dead link to a route that doesn't exist)

### Task 6: Wiring the 3s-auto-dismiss AC to the existing `ToastService`

- [x] `ToastService.show(text: string)` currently hardcodes a 4000ms auto-dismiss (used today only for the product-detail "Lien copié !" toast, Story 3.5). AC #2 specifies 3s for the cart-add confirmation specifically. Add an optional second parameter, `show(text: string, durationMs = 4000)`, and pass `3000` explicitly from `StickyAddToCartComponent`'s success path — the existing "Lien copié !" call site is left unchanged (keeps its current 4s default), so this is additive, not a behavior change to Story 3.5's feature.
  - [x] `ToastComponent`'s existing `role="status"` (`toast.component.ts`) already implicitly maps to `aria-live="polite"` per the ARIA spec — no template change needed there to satisfy AC #2's `aria-live` requirement, just confirm it in a test (Task 7)

### Task 7: Frontend tests

- [x] `cart.store.spec.ts` (`HttpTestingController` pattern from `auth.store.spec.ts`/`product-detail.store.spec.ts`): `loadCart` populates state; `addItem`/`updateItemQuantity`/`removeItem` each replace state from the response; `itemCount` computed sums quantities correctly (including the empty-cart-is-zero case)
- [x] `cart-session.interceptor.spec.ts` (pattern from `auth.interceptor.spec.ts`): attaches the stored session id header when present; does nothing when absent; persists a session id returned in a response header
- [x] `sticky-add-to-cart.component.spec.ts`: renders "Rupture de stock" + `aria-disabled` when `stockQuantity === 0`; calls `CartStore.addItem` and shows a toast on tap when in stock
- [x] `cart-drawer.component.spec.ts`: empty state renders when `items` is empty; Escape closes and restores focus to the trigger; `role="dialog"`/`aria-modal="true"` present
- [x] Per this session's established follow-up (see `catalogue_angular_test_debt` in Dev Notes below): Stories 3.2–3.5 shipped without Angular spec tests as an accepted, deferred gap — this story does NOT repeat that gap; all new components/stores above ship with specs from the start, matching the pattern already used by `auth.store.spec.ts`/`account.store.spec.ts`/`orders.store.spec.ts`.

### Verification

- [x] Task 8: Full verification
  - [x] `npm run build` (production SSR build) and `npm test` (Karma/Jasmine) both green
  - [x] No backend changes expected this story — Story 4.1 already built and tested the full Cart API this story only consumes; if any backend gap is found while wiring the frontend (e.g. a CORS header exposure issue for reading `X-Cart-Session-Id` from the response — see Dev Notes), fix it minimally and note it in Dev Agent Record

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor), run in parallel as background agents against the full diff. Findings below are the synthesis after de-duplication; several were independently raised by more than one reviewer, which is noted explicitly.

### Fixed

1. **Concurrent header-less cart requests could mint two different anonymous sessions, orphaning an item** (Blind Hunter and Edge Case Hunter, independently, same root cause). `CartStore` fires an automatic `GET /api/v1/cart` at construction; if a user-triggered mutation (e.g. `addItem` from the product page) went out before that resolved, and neither request yet had a stored session id, the backend (`Carts.ResolveOwner`) minted a **different** session for each — whichever response's id landed last in `localStorage` won, silently orphaning the other request's cart (and its item) server-side. Fixed at the root: `cartSessionInterceptor` now generates the anonymous session id **client-side, eagerly and synchronously** the first time it runs with none stored yet (`crypto.randomUUID()`), rather than waiting on the server to mint one. Since JS has no true concurrency, the first of any two synchronously-triggered requests to reach the interceptor generates+persists the id before the second one's interceptor call even runs — every request from then on carries the same id, closing the race entirely. The server-side minting fallback (`Carts.ResolveOwner`) is kept as defense in depth but is no longer the primary mechanism.
2. **`CartStore` had no stale-response guard, unlike `ProductDetailStore`'s established pattern** (Blind Hunter and Edge Case Hunter, independently). `loadCart`/`addItem`/`updateItemQuantity`/`removeItem` each unconditionally replaced `items`/`totalInCents` from their own response with no ordering guarantee — an out-of-order response (e.g. the auto `loadCart()` resolving after a user's `addItem()`) could silently revert the cart to a stale snapshot. Fixed with a single shared `requestId` counter across all four methods (same technique as `ProductDetailStore`, extended to a single counter since all four mutate the same state) — a response is only applied if no newer request has been issued since.
3. **Quantity stepper collapsed rapid clicks into one net change** (Blind Hunter and Edge Case Hunter, independently). `increment`/`decrement` computed an absolute target (`item.quantity ± 1`) from the currently-rendered value with no optimistic update and no disabling of the buttons while a request was in flight — two rapid clicks both read the same stale quantity and both PATCHed the same target. Fixed by binding `[disabled]="cartStore.isLoading()"` on the stepper and remove buttons, preventing a second click from firing until the first mutation's response has updated the rendered quantity.
4. **No slide-in transition on the CartDrawer** (Acceptance Auditor — AC #3 gap). The panel was inserted/removed instantly via `@if` with no transition/transform anywhere, despite AC #3 requiring it to "slide in" and the task list claiming a CSS transform transition was used. Fixed with `@keyframes` (mobile: `translateY`, desktop: `translateX`, switched via a media query at the `lg` breakpoint) applied to the panel — since `@if` creates a fresh element on every open, the animation reliably plays from its 0% state every time, the same technique already used for the header badge pulse. Enter-only, by design: AC #3 only specifies an animated open, not an animated close, and an exit animation would need `@angular/animations` (a new dependency) to hook into Angular's structural-directive removal.
5. **Badge pulsed on cart hydration, not just on real user-triggered adds** (Blind Hunter). The pulse effect compared `itemCount()` against a `previousItemCount` seeded at 0, so a returning visitor whose `CartStore` hydrates via its async `GET /api/v1/cart` into an already non-empty cart would see the badge pulse on load — indistinguishable, from the effect's point of view, from a real add. Fixed by seeding `previousItemCount` as `null` (not `0`) and skipping the pulse check entirely on the first observation, regardless of its value; only observations after that first one can trigger a pulse.
6. **`CartStore.error` was set on every failure path but never displayed anywhere** (Edge Case Hunter). A failed add/update/remove left `isLoading`/`isAdding` reset with the item list silently unchanged — zero user-visible feedback. Fixed by having `StickyAddToCartComponent` and `CartDrawerComponent` show a `ToastService` error toast whenever their respective `CartStore` call returns `false` (`updateItemQuantity`/`removeItem` were changed from `Promise<void>` to `Promise<boolean>` to match `addItem`'s existing convention, so callers can react to failure).
7. **Sticky-add-to-cart button had no visual disabled state** (Edge Case Hunter). Only `aria-disabled` was set (deliberately, per AC #6, not the native `disabled` attribute) but nothing bound the Tailwind `disabled:opacity-50` class, since that only fires on the native `:disabled` pseudo-class — the "Rupture de stock" and in-flight states looked fully clickable. Fixed with explicit `[class.opacity-50]`/`[class.pointer-events-none]` bindings alongside the existing `aria-disabled` and JS click-guard.

All fixes verified together: `ng build` (production SSR, 9 prerendered routes) and `npx ng test --watch=false --browsers=ChromeHeadless` both green — 94/94 tests (13 new/updated this round, on top of Task 7's original coverage). Backend `dotnet build MonEcommerce.sln` re-confirmed 0 errors/warnings (no backend changes from this review round).

## Dev Notes

### Confirmed backend gap, fixed: exposing `X-Cart-Session-Id` to the browser

As anticipated: `Program.cs`'s Development CORS policy had `.AllowAnyHeader()` (covers what the browser may SEND) but no `WithExposedHeaders` (governs what the browser's JS may READ off a cross-origin response) — the Angular dev server (`:4200`) and API (`:5287`) are cross-origin, so `cartSessionInterceptor` would have been unable to read a freshly-generated `X-Cart-Session-Id` off the response, silently breaking anonymous-cart persistence (a new session id minted on every request, cart appearing to "reset" each time). Fixed with a one-line addition — `.WithExposedHeaders("X-Cart-Session-Id")` — to the existing `app.UseCors(...)` call in `Program.cs`. Confirmed via full backend rebuild (0 errors) alongside the rest of this story's frontend work; not something Story 4.1 could have caught, since it shipped with no browser client to exercise the cross-origin exposure question.

### Established Angular conventions this story must follow (from Stories 2.x–3.x)

- Standalone components only, no `NgModule` — every existing component/store in this codebase follows this (CLAUDE.md)
- Signal Store (`@ngrx/signals`) for all client state — `signalStore({ providedIn: 'root' }, withState, withMethods, ...)`, see `auth.store.ts`/`product-detail.store.ts`
- SSR-safety: any `localStorage`/`document`/`window` access must be guarded by `isPlatformBrowser(inject(PLATFORM_ID))` — established in `auth.store.ts` and `auth.interceptor.ts`; this story's `cart.store.ts` and `cart-drawer.component.ts` both touch browser-only APIs (`localStorage`, `document.body`) and must follow the same guard
- `ToastService`/`ToastComponent` already exist and are mounted globally in `app.component.html` — reuse them (Task 6), don't build a second toast mechanism
- No `tailwind.config.js` — this is Tailwind v4, tokens are CSS custom properties in `styles.scss`'s `@theme` block (`--color-accent`, `--radius-card`, `--breakpoint-lg`, etc.) — reuse these tokens, don't hardcode hex values in component styles

### `catalogue_angular_test_debt` — this story does not add to it

A prior session decision (documented in this project's persistent memory, not a repo file) explicitly deferred backfilling Angular spec tests for Stories 3.2–3.5's components rather than blocking Epic 4 on it. That deferral was scoped to *those specific, already-shipped* components — it is not a standing license to skip tests going forward. This story's Task 7 ships specs alongside every new file, consistent with how `auth.store.ts`/`account.store.ts`/`orders.store.ts` were already tested in earlier stories.

## Project Structure Notes

New `core/components/header/`, `core/components/cart-drawer/` (global shell pieces, parallel to the existing `core/components/toast/`), `core/interceptors/cart-session.interceptor.ts` (parallel to `core/interceptors/auth.interceptor.ts`), `features/cart/cart.store.ts` (new feature area, parallel to `features/auth/`, `features/account/`), `features/catalogue/components/sticky-add-to-cart/` (parallel to the existing `product-card/`, `product-gallery/` components in the same folder).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.2 acceptance criteria (Epic 4 section)
- `_bmad-output/planning-artifacts/ux-design-specification.md#Component Strategy` (StickyAddToCart/CartDrawer anatomy, states, variants), `#Responsive Design & Accessibilité` (breakpoint adaptations, WCAG strategy)
- `_bmad-output/implementation-artifacts/4-1-panier-anonyme-et-gestion-articles.md` — the backend API this story consumes; specifically its Dev Notes on the `X-Cart-Session-Id` header contract and the explicit deferral of client-side session-id generation to this story
- `frontend/mon-ecommerce-web/src/app/features/auth/auth.store.ts`, `core/interceptors/auth.interceptor.ts` — the Signal Store / interceptor / SSR-guard patterns this story's new code must match
- `frontend/mon-ecommerce-web/src/styles.scss` — design token source of truth (colors, radii, breakpoints)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `@ngrx/signals`'s `withComputed` requires each property to be an actual `Signal` (created via `computed(...)`), not a plain arrow function — `itemCount: () => ...` failed the Angular template compiler (`NG1`/`TS2322` errors) until changed to `itemCount: computed(() => ...)`.
- Confirmed and fixed the anticipated CORS gap: `Program.cs`'s Development policy was missing `WithExposedHeaders("X-Cart-Session-Id")` — see Dev Notes.
- `ng build` (production SSR) and `npx ng test --watch=false --browsers=ChromeHeadless`: both green — 81/81 Karma tests pass, 9 static routes prerendered successfully. Backend `dotnet build MonEcommerce.sln` (Docker, .NET 9 SDK) confirmed 0 errors/warnings after the `Program.cs` CORS change.
- Post-review: re-ran both suites after applying all 7 review fixes — `ng build` still green, `ng test` 94/94 green (13 new/updated tests: `header.component.spec.ts` new entirely, plus additions to `cart-drawer.component.spec.ts`, `sticky-add-to-cart.component.spec.ts`, `cart-session.interceptor.spec.ts`). One own test bug caught and fixed along the way: an initial `header.component.spec.ts` assertion checked the whole header's `textContent` was empty when the cart is empty, which doesn't account for the site name text — narrowed to checking the badge `<span>` specifically.

### Completion Notes List

- Built the minimal site header (Task 0) — no header/nav existed anywhere in the app before this story; scoped to exactly what AC #3 needs (logo link + cart icon/badge), not a full site nav.
- Installed `@angular/cdk@^19.2.19`; used `cdkTrapFocus` (without `cdkTrapFocusAutoCapture`) plus manual, synchronous focus capture/restore in `CartDrawerComponent` to avoid a real race between CDK's own async auto-capture and capturing the true external trigger element — see the Task 5 implementation-deviation note above.
- `CartStore` hydrates via an automatic `GET /api/v1/cart` on construction (browser-only); `cart.store.spec.ts` and `app.component.spec.ts` both account for this by flushing the initial request before asserting further behavior.
- `ToastService.show()` gained an optional `durationMs` parameter (default unchanged at 4000ms) so the cart-add confirmation can use the AC's specific 3000ms without touching Story 3.5's existing "Lien copié !" call site.
- Fixed a real, previously-latent CORS gap in `Program.cs` (see Dev Notes) — required for the anonymous session id to actually persist across requests.
- All new components/stores/interceptors ship with specs (27 new tests across `cart.store.spec.ts`, `cart-session.interceptor.spec.ts`, `sticky-add-to-cart.component.spec.ts`, `cart-drawer.component.spec.ts`, plus `app.component.spec.ts` updates) — no test debt added.
- Checkout ("Passer commande") button in `CartDrawerComponent` is intentionally inert (no navigation) — Story 4.3+'s scope.
- Post-review: applied all 7 fixes from the 3-layer adversarial review (see Review Findings) — the session-id race fix (client-side eager generation) and the `CartStore` staleness guard were the two highest-severity findings, each independently confirmed by two reviewers. Added `header.component.spec.ts` (didn't exist before this round) specifically to cover the badge-hydration-pulse fix and prevent regression.

### File List

**Backend**
- `backend/MonEcommerce/src/Web/Program.cs` (modified — `WithExposedHeaders("X-Cart-Session-Id")` on the Development CORS policy)

**Frontend — core**
- `frontend/mon-ecommerce-web/src/app/core/constants/storage-keys.ts` (modified — added `CART_SESSION_ID_KEY`)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/cart-session.interceptor.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/interceptors/cart-session.interceptor.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/components/header/header.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/core/components/header/header.component.spec.ts` (new — added during the review round)
- `frontend/mon-ecommerce-web/src/app/core/components/cart-drawer/cart-drawer.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/core/components/cart-drawer/cart-drawer.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts` (modified — optional `durationMs` parameter)
- `frontend/mon-ecommerce-web/src/app/app.config.ts` (modified — registered `cartSessionInterceptor`)
- `frontend/mon-ecommerce-web/src/app/app.component.ts`, `.html` (modified — mounted `HeaderComponent`/`CartDrawerComponent`)
- `frontend/mon-ecommerce-web/src/app/app.component.spec.ts` (modified — accounts for `CartStore`'s auto-fetch on construction)

**Frontend — features**
- `frontend/mon-ecommerce-web/src/app/features/cart/cart.store.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/cart/cart.store.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/sticky-add-to-cart/sticky-add-to-cart.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/sticky-add-to-cart/sticky-add-to-cart.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.ts`, `.html` (modified — mounted `StickyAddToCartComponent`)

**Dependencies**
- `frontend/mon-ecommerce-web/package.json`, `package-lock.json` (modified — added `@angular/cdk@^19.2.19`)
