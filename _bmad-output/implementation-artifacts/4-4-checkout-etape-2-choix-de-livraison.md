# Story 4.4: Checkout Étape 2 — Choix de Livraison

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to choose a delivery method (Standard / Express) with visible pricing and estimated delay,
so that I can control the cost and timing of my order delivery.

## Acceptance Criteria

1. **Given** the customer reaches checkout step 2, **when** `GET /api/v1/shipping-options` is called, **then** available options are returned with name, price (cents), and estimated delay (e.g., "3–5 jours ouvrés").
2. **Given** a shipping option is selected, **when** the selection changes, **then** the order subtotal updates in real time to reflect the shipping cost.
3. **Given** the customer clicks "Continuer", **when** a shipping option is selected, **then** the selection is saved and the customer proceeds to step 3.
4. The `OrderStepIndicator` shows "Étape 2/4 — Livraison" as active.
5. The previously selected option is preserved if the customer navigates back.
6. At least one shipping option is always available (Standard is always shown).
7. Flutter mobile: **[Scoped — see Story 4.3's Dev Notes]** out of scope, same standing decision as Story 4.3 (no Flutter cart/checkout entry point exists; not re-litigated here).

## Tasks / Subtasks

### Backend — new read-only endpoint (AC: #1, #6)

- [x] Task 1: `GetShippingOptionsQuery` (AC: #1, #6)
  - [x] New `Application/Shipping/Models/ShippingOptionDto.cs`: `record ShippingOptionDto(string Id, string Name, int PriceInCents, string EstimatedDelay)` — `Id` is a stable string slug (`"standard"`/`"express"`), not a `Guid`: these are fixed, hardcoded options, not a DB-backed, admin-manageable catalog (no story anywhere in the backlog asks for shipping-option CRUD/administration — building persistence for it now would be speculative scope)
  - [x] New `Application/Shipping/Queries/GetShippingOptionsQuery.cs` + Handler: no parameters, returns a hardcoded `List<ShippingOptionDto>` directly from the handler — two options: `standard` ("Livraison Standard", 490 cents, "3–5 jours ouvrés") and `express` ("Livraison Express", 990 cents, "1–2 jours ouvrés"). Exact prices/delays aren't specified anywhere in the PRD/epics beyond the AC's own example delay string — these are reasonable, defensible placeholder values, not a business decision requiring escalation
  - [x] `standard` is always first in the returned list and always present — trivially satisfies AC #6 since the list is hardcoded, not filtered/queried from mutable data that could ever come back empty
- [x] Task 2: `Web/Endpoints/ShippingOptions.cs` (AC: #1)
  - [x] `RoutePrefix => "/api/v1/shipping-options"`, single `MapGet(GetShippingOptions).AllowAnonymous()` — same public-data reasoning as `Products.cs`'s catalogue endpoints: the options are identical for every customer, nothing user-specific, no reason to require authentication just to list them (checkout as a whole still requires auth via `authGuard` client-side, same as Story 4.3)
- [x] Task 3: Backend tests
  - [x] `GetShippingOptionsQueryHandlerTests.cs`: returns exactly 2 options; `standard` is present and first; every option has a non-empty `Name`/positive `PriceInCents`/non-empty `EstimatedDelay`

### Frontend — Angular only (AC: #7)

- [x] Task 4: Extend `CheckoutStore` (AC: #3, #5)
  - [x] Add `shippingOption: ShippingOption | null` to `CheckoutState` (parallel to Story 4.3's `address` field) — `ShippingOption` mirrors `ShippingOptionDto`'s shape exactly
  - [x] Add `loadShippingOptions(): Promise<void>` (populates a new `shippingOptions: ShippingOption[]` state list via `GET /api/v1/shipping-options`) and `setShippingOption(option: ShippingOption): void` (plain `patchState`, same pattern as `setAddress`)
  - [x] Root-provided store already satisfies AC #5 for free, same mechanism as Story 4.3's address — no new persistence work needed
- [x] Task 5: Checkout shipping page (AC: #1, #2, #3, #4, #6)
  - [x] New `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-shipping/checkout-shipping.component.ts` (parallel naming to Story 4.3's `checkout-address`, not the stale `pages/shipping` sketch in `architecture.md` — consistency with the already-shipped sibling page wins over an early planning doc)
  - [x] On init: call `CheckoutStore.loadShippingOptions()`; pre-select `CheckoutStore.shippingOption()` if already set (AC #5), else default-select nothing (the customer must actively choose, even though `standard` is always first in the list — don't silently auto-select a paid choice on their behalf)
  - [x] Radio-button list of options (name, price, estimated delay) — selecting one updates a local/store-driven "selected" state immediately
  - [x] Live subtotal (AC #2): `CartStore.totalInCents() + (selected option's priceInCents ?? 0)`, recomputed reactively as the selection changes — read `CartStore` directly (already exists, already loaded app-wide since Story 4.1/4.2), don't duplicate cart-total state in `CheckoutStore`
  - [x] "Continuer" button disabled until an option is selected (AC #3's precondition — matches the AC's own framing: "when a shipping option is selected")
  - [x] On submit: `CheckoutStore.setShippingOption(...)` then `router.navigate(['/checkout/paiement'])` (Story 4.5's not-yet-built route — same "navigate to a route that doesn't exist yet" pattern already used by Story 4.3's step 1 → step 2 link)
  - [x] Mount `<app-order-step-indicator [currentStep]="2" />` at the top of the page
- [x] Task 6: Routing (AC: #1)
  - [x] New route `checkout/livraison` in `app.routes.ts`, `canActivate: [authGuard]` — same reasoning as Story 4.3 (checkout requires authentication); this is also the exact path Story 4.3's `CheckoutAddressComponent.onSubmit()` already navigates to, so step 1 → step 2 becomes a real, working transition for the first time
- [x] Task 7: Frontend tests
  - [x] `checkout.store.spec.ts` additions: `loadShippingOptions` populates `shippingOptions`; `setShippingOption` patches `shippingOption`
  - [x] `checkout-shipping.component.spec.ts`: fetches and renders options on init; pre-selects an existing `CheckoutStore.shippingOption()` if present; updates the displayed subtotal when the selection changes (cart total + shipping cost); "Continuer" is disabled with no selection and enabled once one is picked; calls `setShippingOption` and navigates to `/checkout/paiement` on submit

### Verification

- [x] Task 8: Full verification
  - [x] Backend: `dotnet build` + `dotnet test` (Docker, .NET 9 SDK) green
  - [x] Frontend: `npm run build` (production SSR) + `npm test` (Karma/Jasmine) green
  - [x] Post-review: re-verified both suites after all fixes (see Review Findings)

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor), run in parallel as background agents against the full diff. Findings below are the synthesis after de-duplication.

### Fixed

1. **`(ngSubmit)` was dead code — clicking "Continuer" would have triggered a native full-page reload, not `onSubmit()`** (Blind Hunter — the single most severe finding). `checkout-shipping.component.html`'s `<form (ngSubmit)="onSubmit()">` relies on `NgForm`, which only attaches when `FormsModule`/`ReactiveFormsModule` is imported — this component imported neither. Angular silently accepts `(ngSubmit)` as a no-op custom DOM event listener with no compile error, so the bug was completely invisible until a real click. **This means AC #3 was actually broken in real usage despite the original unit test passing** — that test called `component.onSubmit()` directly, bypassing the broken template wiring entirely, which is exactly why the 3-layer review process caught what a single reviewer plus a passing test suite did not. Fixed by dropping `(ngSubmit)`/`type="submit"` entirely (there's no `FormGroup` here to justify pulling in a Forms module) in favor of a direct `(click)="onSubmit()"` on a `type="button"` button — simpler and no new dependency. The test was also fixed to click the real button (`continueButton(fixture).click()`) instead of calling `onSubmit()` directly, so this exact class of bug can't hide behind a passing test again.
2. **A failed shipping-options fetch was a dead end with no recovery** (Edge Case Hunter). `shippingOptionsError` was set on failure, but `shippingOptions` stayed empty, so the radio list rendered nothing, "Continuer" stayed permanently disabled, and there was no retry mechanism — only a full page reload could recover. Fixed with a "Réessayer" button that re-calls `loadShippingOptions()`.
3. **Stale cached options + a fresh error, shown together, contradicted each other** (Edge Case Hunter). If options loaded successfully once (cached in the root-provided `CheckoutStore`) and a later re-fetch (e.g. returning from step 1) failed, the error banner rendered directly above a fully usable, already-populated radio list — confusing, though not actually broken. Fixed by only showing the error banner when there are also zero options to fall back on (`shippingOptionsError() && shippingOptions().length === 0`); stale-but-valid data now takes priority over a redundant error message.
4. **No step-order guard — `/checkout/livraison` was reachable directly (bookmark, typed URL) with no address ever set** (Edge Case Hunter). Nothing crashed, but a customer could silently skip the address step entirely. Fixed with a check in `ngOnInit`: no `CheckoutStore.address()` → redirect to `/checkout/adresse` before ever calling `loadShippingOptions()`.
5. **Backend: the hardcoded options list was exposed as a mutable `List<T>` shared across every request for the process lifetime** (Blind Hunter). Nothing currently mutates it, but the shared static field was one accidental `.Sort()`/`.Add()` away (a future pipeline behaviour, decorator, etc.) from permanently corrupting the list for every subsequent caller until an app restart. Fixed by changing the query/handler's return type to `IReadOnlyList<ShippingOptionDto>`.
6. **Misleading code comment** (Blind Hunter). A comment on the `selected` signal claimed the customer "must actively pick an option... not as an auto-confirmed choice bypassing the Continuer gate," which doesn't match what the code actually does (a pre-existing store selection enables "Continuer" immediately with zero clicks — correct, desirable behavior for AC #5, just incorrectly described). Reworded to describe the actual behavior.

All fixes verified together: backend `dotnet test` — 161/161 (unchanged count; the `IReadOnlyList` change needed no new tests). Frontend `ng build`/`ng test` — 122/122 (up from 120; net +2 after adding the guard test, the retry test, and fixing the submit test to use a real click instead of a direct method call, which removed the one false-positive test and replaced it with a genuine one).

## Dev Notes

### Why a hardcoded list, not a database table

`Order` (`Domain/Entities/Order.cs`) has no shipping-related fields at all today (no `ShippingMethod`/`ShippingCostInCents`) — recording the customer's actual chosen shipping cost against a real order is Story 4.6's concern (order creation), not this story's, following the exact same "defer persistence to order creation" reasoning already established in Story 4.3's Dev Notes for the address step. Nothing in the PRD or epics describes shipping options as an admin-manageable catalog (no CRUD story exists for it anywhere in the backlog), so a hardcoded, in-code list is the proportionate choice — not a database table, migration, or admin UI that nothing asks for.

### Flutter mobile — same standing decision as Story 4.3

See AC #7 and Story 4.3's Dev Notes / the linked persistent-memory decision (`epic4_flutter_checkout_gap`). Not re-litigated here — Stories 4.4-4.6 all follow the same Angular-only scoping the user already chose once for the whole checkout epic.

### Established conventions this story must follow

- `CheckoutStore` (Story 4.3): extend it in place, don't create a second checkout-state store
- `CartStore` (Story 4.1/4.2, `features/cart/cart.store.ts`): already holds `totalInCents` app-wide — read it directly for the live-subtotal requirement, don't duplicate cart state
- `OrderStepIndicator` (Story 4.3): reuse as-is with `[currentStep]="2"` — it already clamps out-of-range input and renders the "Étape X/4 — Label" caption generically
- `authGuard` (`core/guards/auth.guard.ts`): reuse directly for the new route, same as Story 4.3
- `IEndpointGroup`/`Web/Endpoints/Products.cs`'s public-GET pattern (`.AllowAnonymous()` on non-sensitive, non-user-specific reads): follow directly for `ShippingOptions.cs`

## Project Structure Notes

New `Application/Shipping/` (Models, Queries) on the backend, parallel to `Application/Carts/`. New `Web/Endpoints/ShippingOptions.cs`, parallel to `Products.cs`. New `frontend/.../features/checkout/pages/checkout-shipping/`, parallel to Story 4.3's `pages/checkout-address/`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.4 acceptance criteria (Epic 4 section)
- `_bmad-output/implementation-artifacts/4-3-checkout-etape-1-adresse-de-livraison.md` — `CheckoutStore`/`OrderStepIndicator`/`authGuard` conventions this story extends directly; the Flutter-scope decision this story inherits without re-asking
- `backend/MonEcommerce/src/Domain/Entities/Order.cs` — confirms no shipping fields exist yet, supporting the "defer persistence to Story 4.6" decision
- `backend/MonEcommerce/src/Web/Endpoints/Products.cs` — the public-GET endpoint pattern to follow
- `frontend/mon-ecommerce-web/src/app/features/cart/cart.store.ts` — the existing cart-total source this story's live subtotal reads from

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet test MonEcommerce.sln` (Docker, .NET 9 SDK) — 161/161 passed (was 158/158; 3 new tests for `GetShippingOptionsQueryHandler`).
- Frontend: `ng build` (production SSR, 11 static routes, up from 10) and `npx ng test --watch=false --browsers=ChromeHeadless` — 120/120 passed (was 111/111; 9 new tests: 3 `checkout.store.spec.ts` additions, 6 `checkout-shipping.component.spec.ts`).
- Post-review: fixed a critical wiring bug — `(ngSubmit)` was dead code with no `FormsModule` imported, meaning the original 120/120-green state still shipped a "Continuer" button that would have triggered a native page reload on real user interaction (the test suite couldn't have caught it: the original test called `onSubmit()` directly). Re-verified after the fix with a test that performs a real DOM `.click()` instead. Final: backend 161/161 (unchanged), frontend 122/122.

### Completion Notes List

- Backend: `GET /api/v1/shipping-options` returns a hardcoded 2-option list (no database table — see Dev Notes for why); `AllowAnonymous`, same public-data reasoning as the catalogue endpoints.
- `CheckoutStore` extended (not replaced) with `shippingOptions`/`shippingOption`/`shippingOptionsError` state and `loadShippingOptions()`/`setShippingOption()` methods — added proactive error handling for the fetch (not explicitly required by any AC, but applying the exact lesson from Story 4.3's review: a silently-swallowed HTTP failure there was a real, reviewer-caught bug, so this store gets it right the first time instead of waiting for another review round to catch it again).
- `CheckoutShippingComponent`'s live subtotal reads `CartStore.totalInCents()` directly (no duplicated cart state in `CheckoutStore`) via a `computed()` combining it with the locally-selected option's price.
- Step 1 → step 2 is now a real, working transition: Story 4.3's `CheckoutAddressComponent` already navigated to `/checkout/livraison`, which didn't exist as a route until this story added it.
- Route naming (`pages/checkout-shipping/`) follows the precedent already set by Story 4.3's `pages/checkout-address/`, not the earlier, now-stale `pages/shipping` sketch in `architecture.md`.
- Post-review: fixed 6 findings from the 3-layer adversarial review — most notably a dead `(ngSubmit)` binding that meant "Continuer" didn't actually work in real browser usage despite a green test suite (see Review Findings for the full list, including two backend hardening fixes and two step-2 UX gaps).

### File List

**Backend**
- `backend/MonEcommerce/src/Application/Shipping/Models/ShippingOptionDto.cs` (new)
- `backend/MonEcommerce/src/Application/Shipping/Queries/GetShippingOptionsQuery.cs` + Handler (new)
- `backend/MonEcommerce/src/Web/Endpoints/ShippingOptions.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Shipping/Queries/GetShippingOptionsQueryHandlerTests.cs` (new)

**Frontend**
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.ts` (modified — added shipping state/methods)
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.spec.ts` (modified — added shipping tests, now uses `provideHttpClient`/`provideHttpClientTesting`)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-shipping/checkout-shipping.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-shipping/checkout-shipping.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — added `checkout/livraison` route, `authGuard`-protected)
