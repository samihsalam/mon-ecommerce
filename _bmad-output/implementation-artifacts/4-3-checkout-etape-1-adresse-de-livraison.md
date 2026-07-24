# Story 4.3: Checkout Étape 1 — Adresse de Livraison

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to enter or select my delivery address as the first checkout step,
so that I can begin the order process with my shipping details confirmed.

## Acceptance Criteria

1. **Given** a logged-in customer with a saved address, **when** they reach checkout step 1, **then** their saved address is pre-filled in the form.
2. **Given** the address form is displayed, **when** a field loses focus with invalid data, **then** an inline error appears below the field in `#C0564A` with `aria-describedby` linked.
3. **Given** the form is valid and submitted, **when** the customer clicks "Continuer", **then** the address is saved to the session and the customer proceeds to step 2.
4. The `OrderStepIndicator` shows "Étape 1/4 — Adresse" as active.
5. Required fields are: street, city, postal code, country.
6. Form data is auto-saved between steps (no data loss on back navigation).
7. The form is accessible on both Angular web and Flutter mobile. **[Scoped — see Dev Notes]**: per explicit user decision, this story (and 4.4-4.6) ship Angular web only. Flutter has no cart UI or checkout entry point at all yet (Epic 4's backlog never scheduled one, unlike Epic 3's explicit web+mobile story pairing) — a Flutter checkout screen would be unreachable from normal app navigation. Tracked as a deferred gap, not silently dropped.

## Tasks / Subtasks

### Backend — none needed this story (see Dev Notes)

- [x] Task 0: Confirm no backend changes are required
  - [x] `GET /api/v1/account/profile` (Story 2.x, `Web/Endpoints/Account.cs`) already returns `Addresses: AddressDto[]` for the authenticated user — sufficient for AC #1's pre-fill, no new endpoint needed
  - [x] "Saved to the session" (AC #3) is client-side checkout-wizard state, not a new `Address` row — no create/update-address command exists in `Application/Account/` today, and none is added by this story; the collected address data is carried through the wizard's own state and only becomes a persisted `Address`/`Order.ShippingAddressId` when the order is actually created (Story 4.6's scope) — see Dev Notes

### Task 1: `OrderStepIndicator` component (AC: #4)

- [x] New `frontend/mon-ecommerce-web/src/app/features/checkout/components/order-step-indicator/order-step-indicator.component.ts` — first use, shared by Stories 4.3-4.6
  - [x] Input: current step index (1-4) and step labels (`['Adresse', 'Livraison', 'Paiement', 'Confirmée']`)
  - [x] Three visual states per step (UX spec): completed (gold `--color-accent` + check icon), active (black `--color-text`), upcoming (gray `--color-text-secondary`) — connecting line between steps
  - [x] `aria-current="step"` on the active step (per `epics.md`'s UX-DR11 requirement), plain text label on each step (not just a number) for screen readers
  - [x] Responsive: horizontal with labels on desktop, compact dots on mobile (`lg:` breakpoint, matching the UX spec's documented adaptation)

### Task 2: `CheckoutStore` (AC: #3, #6)

- [x] New `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.ts` — `signalStore({ providedIn: 'root' }, withState, withMethods)`, same shape as `AuthStore`/`CartStore`
  - [x] State: `{ address: CheckoutAddress | null }` (shipping/payment fields added by Stories 4.4/4.5 — don't build them now, `undefined` tasks belong to those stories)
  - [x] `CheckoutAddress` shape mirrors `AddressDto`'s fields: `{ street: string; city: string; postalCode: string; country: string }` (no `id` — this is a plain value being carried through the wizard, not a persisted entity reference)
  - [x] `setAddress(address: CheckoutAddress): void` — a plain synchronous `patchState`, no HTTP call (see Task 0)
  - [x] Root-provided (not route-scoped), so state naturally survives Angular Router navigation between checkout steps with zero extra persistence work — satisfies AC #6's "no data loss on back navigation" for free. Deliberately NOT persisted to `sessionStorage`/`localStorage`: AC #6 only requires surviving back-navigation within the app, not a hard page refresh, and adding storage persistence for an unrequired case would be speculative scope

### Task 3: Address form page (AC: #1, #2, #3, #5)

- [x] New `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-address/checkout-address.component.ts`
  - [x] Reactive Forms (`FormBuilder.nonNullable.group`), same pattern as `register.component.ts`/`profile.component.ts` — NOT `updateOn: 'blur'` (Story 2.1's established reasoning: Enter-key staleness)
  - [x] Fields: `street`, `city`, `postalCode`, `country`, each `Validators.required` (AC #5's required-fields list; no format validation specified beyond required, so don't invent stricter rules)
  - [x] Pre-fill (AC #1): on init, call `AccountStore.loadProfile()` (already exists, already used by `ProfileComponent`) and patch the form from `profile.addresses[0]` if any address exists; if `CheckoutStore.address()` is already set (returning to this step from step 2 — AC #6), prefer THAT over the account's saved address, since it reflects what the customer already confirmed for THIS order (may differ from their default saved address)
  - [x] Inline errors (AC #2): exact existing pattern from `register.component.html` — `text-error` class (maps to `--color-error: #C0564A` in `styles.scss`) on a `<p>` below the field, shown when `control.invalid && control.touched`, with `[attr.aria-describedby]` conditionally set to the error paragraph's id
  - [x] On submit: if valid, call `CheckoutStore.setAddress(...)` then `router.navigate(['/checkout/livraison'])` (Story 4.4's not-yet-built route — navigating there is fine, Angular will 404/no-op gracefully until that story adds it; do NOT build a placeholder step 2 page as part of this story, that's scope creep)
  - [x] Mount `<app-order-step-indicator [currentStep]="1" />` at the top of the page

### Task 4: Routing (AC: #1)

- [x] New route `checkout/adresse` in `app.routes.ts`, `canActivate: [authGuard]` — checkout requires an authenticated user: `Domain/Entities/Address.cs`'s `UserId` is a required (non-nullable) `string`, so there is no schema-valid way to attach an address to an anonymous session; matches AC #1's own framing ("a logged-in customer")

### Task 5: Frontend tests

- [x] `checkout.store.spec.ts`: `setAddress` patches state; state survives independent of any HTTP mocking (it's pure client state)
- [x] `checkout-address.component.spec.ts`: pre-fills from `AccountStore`'s loaded profile when no `CheckoutStore` address exists yet; prefers an existing `CheckoutStore` address over the account's saved one when both exist; shows inline errors with `aria-describedby` on blur for each required field; calls `CheckoutStore.setAddress` and navigates to `/checkout/livraison` on valid submit; does NOT navigate when the form is invalid
- [x] `order-step-indicator.component.spec.ts`: renders the correct active/completed/upcoming state per step; sets `aria-current="step"` only on the active step

### Verification

- [x] Task 6: Full verification
  - [x] `npm run build` (production SSR) and `npm test` (Karma/Jasmine) both green
  - [x] No backend changes — confirm `dotnet build`/`dotnet test` still pass unchanged (nothing in this story touches the backend)
  - [x] Post-review: one backend fix ended up necessary after all (see Review Findings) — re-verified `dotnet build`/`dotnet test` after it

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor), run in parallel as background agents against the full diff. Findings below are the synthesis after de-duplication.

### Fixed

1. **Whitespace-only input passed `Validators.required`** (Edge Case Hunter, confirmed against Angular's own source: `isEmptyInputValue` only checks `value == null || length === 0`, no trimming). `street: "   "` had length 3 and validated as "present". Fixed with a small custom `requiredNotBlank()` validator (trims before checking) replacing `Validators.required` on all four fields.
2. **`LoadAddressesAsync` had no `ORDER BY` at all** (Edge Case Hunter). `profile.addresses[0]`, consumed by this story's own pre-fill logic, had no guaranteed or meaningful order across requests — and `Address` had no `Created` timestamp to order by in the first place (it extended `BaseEntity`, not `BaseAuditableEntity`). Currently unreachable in production (confirmed: no code path anywhere creates an `Address` row yet), but a real latent bug this story's own frontend code now semantically depends on. Fixed properly rather than with a cosmetic tie-break: migrated `Address` to `BaseAuditableEntity` (matching every other user-facing list entity in this codebase, e.g. `Order`) and added `OrderByDescending(a => a.Created).ThenByDescending(a => a.Id)` to `LoadAddressesAsync` — the exact same convention already used by `GetOrdersAsync` in the same file. New migration `AddAddressAuditableFields`; safe on the currently-empty `Addresses` table.
3. **`OrderStepIndicator` rendered the literal text "undefined" for an out-of-range `currentStep`** (Edge Case Hunter). Not reachable today (the only call site hardcodes `1`), but Stories 4.4-4.6 will pass 2/3/4, and nothing guarded against 0 or >4. Fixed with a `clampedStep` getter (`Math.min(Math.max(currentStep(), 1), labels.length)`) used everywhere the template previously read `currentStep()` directly.
4. **Silent failure when the account-profile pre-fill fetch fails** (Blind Hunter). `AccountStore.loadProfile()` swallows HTTP errors internally and never rejects, so the form always became usable — but `accountStore.error()` was never read or rendered anywhere on this page, unlike the established pattern in `ProfileComponent` (which does render it). A customer with a real saved address, hitting this during a backend outage, would see a silently empty form with zero indication their address didn't load. Fixed by rendering `accountStore.error()` as a dismissable-by-context notice above the form (the form itself stays usable either way, so the customer can still type a fresh address).

All fixes verified together: `ng build` (SSR) / `ng test` — 111/111 (up from 107; 4 new/updated tests for the two frontend fixes). Backend `dotnet test MonEcommerce.sln` — 158/158 (up from 153; 1 new regression test for the address-ordering fix), plus a fresh `dotnet build` confirming the `Address` entity/migration change compiles cleanly.

## Dev Notes

### Why no backend changes: "saved to the session" is client-side wizard state, not a persisted `Address`

The AC's literal wording ("the address is saved to the session") was checked against the actual schema and application layer before assuming a new command was needed: `Domain/Entities/Address.cs` has no session/draft concept, and `Application/Account/` has no create/update-address command at all today — only read access via `ProfileDto.Addresses` (`AccountService.LoadAddressesAsync`). Building a full create-address command now, only to have Story 4.6 potentially re-model it around actual order creation, would be speculative. Instead: the address the customer confirms at this step is held in a new `CheckoutStore` (client-side only) and carried through Stories 4.4/4.5's steps; Story 4.6 ("Confirmation Commande") is where an actual `Address` row and `Order.ShippingAddressId` reference get created, as part of placing the real order. This keeps this story's scope to exactly what AC #1-#6 ask for, without pre-deciding Story 4.6's persistence model.

### Flutter mobile scope — deferred, not dropped

See AC #7. No Flutter cart or checkout screens exist anywhere in this codebase, and Epic 4's backlog (`epics.md`) never scheduled a Flutter-specific story for any of Stories 4.1-4.6, unlike Epic 3's explicit web/mobile pairing (e.g. Story 3.3 web + Story 3.4 Flutter for the same components). Surfaced to the user via `AskUserQuestion`; the user chose to scope Stories 4.3-4.6 to Angular web only, deferring Flutter checkout as a tracked gap to revisit once Epic 4's web flow is complete — the same class of decision as Epic 3's accepted Angular-test-debt deferral (see that story's Dev Notes precedent).

### Checkout requires authentication

`Address.UserId` is a required, non-nullable `string` in the domain schema — there's no way to persist an address for an anonymous session. This means checkout (starting at this step) requires a logged-in customer, consistent with AC #1's own framing. The anonymous cart (Story 4.1) remains fully usable for browsing/adding items without an account; login is only required at the checkout boundary, a common and expected e-commerce pattern, not a new restriction being introduced here.

### Established Angular conventions this story must follow

- Reactive Forms + the exact inline-error pattern from `features/auth/pages/register/register.component.html` (`text-error`, conditional `aria-describedby`) — do not invent a new validation-display pattern
- Signal Store (`@ngrx/signals`) for `CheckoutStore`, same shape as `AuthStore`/`CartStore`/`AccountStore`
- `AccountStore.loadProfile()` already exists and already returns `addresses` — reuse it directly, don't duplicate a profile-fetch in `CheckoutStore`
- `authGuard` already exists (`core/guards/auth.guard.ts`) and is exactly the right guard for this route — reuse it, don't write a new one

## Project Structure Notes

New `features/checkout/` feature area (parallel to `features/cart/`, `features/account/`): `checkout.store.ts`, `pages/checkout-address/`, `components/order-step-indicator/` (the latter shared by Stories 4.4-4.6, so it lives at the feature root's `components/`, not nested under `pages/checkout-address/`).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.3 acceptance criteria (Epic 4 section); UX-DR11 (`OrderStepIndicator` spec, line ~109)
- `_bmad-output/planning-artifacts/ux-design-specification.md#Component Strategy` — `OrderStepIndicator` anatomy/states/variants
- `backend/MonEcommerce/src/Domain/Entities/Address.cs`, `Application/Account/Models/AddressDto.cs`, `ProfileDto.cs`, `Infrastructure/Identity/AccountService.cs` — the existing address read-path this story's pre-fill relies on; confirms no create/update-address command exists yet
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.html` — the inline-error/`aria-describedby` pattern to replicate exactly
- `frontend/mon-ecommerce-web/src/app/features/account/account.store.ts`, `pages/profile/profile.component.ts` — `AccountStore`/Reactive Forms conventions to follow
- `frontend/mon-ecommerce-web/src/app/core/guards/auth.guard.ts` — reused directly for this story's route

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `ng build` (production SSR) and `npx ng test --watch=false --browsers=ChromeHeadless`: both green — 107/107 Karma tests pass (13 new this story: `checkout.store.spec.ts`, `checkout-address.component.spec.ts`, `order-step-indicator.component.spec.ts`), 10 static routes prerendered (up from 9 — `checkout/adresse` prerenders fine even though it's guarded, since `authGuard` only runs at real navigation time, not during static prerendering).
- No backend changes this story — confirmed by design (Task 0), not just by omission: checked `Application/Account/` for any create/update-address command before concluding none was needed.
- Post-review: one backend change turned out to be necessary after all (see Review Findings #2) — migrated `Address` from `BaseEntity` to `BaseAuditableEntity`, added migration `AddAddressAuditableFields` (generated using the `EFCoreToolsRunning=true` + design-time-factory tooling fix established in Story 4.1), and added an `ORDER BY` to `LoadAddressesAsync`. `dotnet build`/`dotnet test` re-confirmed green: 158/158 (was 153/153).
- Final `ng test`: 111/111 (was 107/107 pre-review) — 4 new/updated tests covering the two frontend review fixes (whitespace validation, profile-error notice, plus 2 out-of-range `OrderStepIndicator` tests).

### Completion Notes List

- Built `OrderStepIndicator` (first use, shared component for Stories 4.3-4.6) with three visual states, a responsive horizontal/dots layout, and a literal "Étape X/4 — Label" caption rendered at every breakpoint (in addition to the desktop per-step labels) to satisfy AC #4's exact wording unambiguously.
- Built `CheckoutStore` as pure client-side state (no HTTP) — deliberately minimal for this story (`address` only); Stories 4.4/4.5 will extend it with shipping/payment fields rather than this story inventing that shape speculatively.
- `CheckoutAddressComponent` prefers an already-set `CheckoutStore` address over the account's saved one (covers AC #6's back-navigation case) and skips the profile fetch entirely in that case — verified via `httpMock.expectNone(...)` in the corresponding test.
- Confirmed via `AskUserQuestion` before starting: Flutter mobile is out of scope for this story (and 4.4-4.6) since Epic 4's backlog never scheduled a Flutter cart/checkout story and Flutter has no cart UI at all yet — see Dev Notes and the linked persistent-memory decision.
- Checkout now requires authentication (`authGuard` on `/checkout/adresse`) — a direct consequence of `Address.UserId` being a required, non-nullable field in the existing schema, not a new business rule being introduced.
- Post-review: fixed 4 findings from the 3-layer adversarial review — a whitespace-only-input validation gap, a missing `ORDER BY` on address lookup (required migrating `Address` to `BaseAuditableEntity`), an out-of-range-step guard on `OrderStepIndicator`, and a silently-swallowed profile-fetch error. See Review Findings.

### File List

**Backend**
- `backend/MonEcommerce/src/Domain/Entities/Address.cs` (modified — now extends `BaseAuditableEntity`)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (modified — `LoadAddressesAsync` ordering)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260724163803_AddAddressAuditableFields.cs` + `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` (new/modified)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceTests.cs` (modified — added ordering regression test)

**Frontend**
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/components/order-step-indicator/order-step-indicator.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/components/order-step-indicator/order-step-indicator.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-address/checkout-address.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-address/checkout-address.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — added `checkout/adresse` route, `authGuard`-protected)
