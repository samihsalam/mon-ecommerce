# Story 8.2: Bannière RGPD & Gestion des Cookies

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to accept or refuse non-essential cookies via a clear banner,
so that I have control over my data in compliance with GDPR.

## Acceptance Criteria

1. Given a first-time visitor with no stored consent, when any page loads, then the RGPD cookie banner is displayed with three options: "Accepter tout", "Refuser", "Personnaliser".
2. Given the visitor clicks "Refuser", when consent is saved, then only strictly necessary cookies are set and no analytics or marketing scripts are loaded.
3. Given the visitor makes a consent choice, when the choice is stored, then it is persisted for 12 months in `localStorage` and the banner does not reappear.
4. No tracking scripts load before consent is given.
5. The banner is fully keyboard accessible (Tab, Enter, Escape).
6. `aria-label` is set on all banner buttons.
7. A "Modifier mes préférences" link in the footer allows consent to be changed at any time.

## Tasks / Subtasks

- [x] Task 1: `ConsentService` (`core/services/consent.service.ts` + `.spec.ts`) (AC #1, #2, #3, #4, #7)
  - [x] Subtask 1.1: Signal-based state (`providedIn: 'root'`, same shape as `ToastService`/`CartStore`): a private `_consent = signal<ConsentRecord | null>(null)` and a public `isBannerOpen` signal (or a `showBanner` computed derived from `_consent() === null`, plus an explicit `open()` to support "Modifier mes préférences" re-opening it after consent already exists).
  - [x] Subtask 1.2: On construction, guarded by `isPlatformBrowser(inject(PLATFORM_ID))` (same SSR guard as `cartSessionInterceptor` and `CartDrawerComponent` — `localStorage` does not exist during SSR), read the stored consent from `localStorage` under a new `CONSENT_KEY` added to `core/constants/storage-keys.ts`. Stored value is JSON: `{ status: 'accepted-all' | 'rejected' | 'custom', nonEssential: boolean, timestamp: number }`. If absent, or if `Date.now() - timestamp > 12 months in ms`, treat as no consent (banner must show) — AC #3's "persisted for 12 months" implies expiry, not permanent storage.
  - [x] Subtask 1.3: `acceptAll()`, `reject()`, `acceptCustom(nonEssential: boolean)` methods — each builds the `ConsentRecord`, writes it to `localStorage` (guarded by `isPlatformBrowser`), and updates the signal so the banner hides reactively (same reactive-close pattern as `CartStore.close()` driving `CartDrawerComponent`'s effect).
  - [x] Subtask 1.4: `hasNonEssentialConsent(): boolean` (or a signal) exposing whether non-essential cookies are currently allowed — this is the gate future analytics/marketing script loaders must check before injecting anything (AC #2, #4). Document in a comment that no analytics script exists yet in this codebase (confirmed: no `gtag`/GA/tracking script anywhere in `frontend/mon-ecommerce-web/src`) — this method is the enforcement point for when one is added later, not retrofitting an existing integration.
  - [x] Subtask 1.5: `reopen()` (or reuse `open()`) for the footer's "Modifier mes préférences" link — must NOT clear the existing stored consent, only re-show the banner UI so the visitor can make a new choice (which then overwrites storage via 1.3's methods).

- [x] Task 2: `CookieBannerComponent` (`core/components/cookie-banner/cookie-banner.component.{ts,html,scss,spec.ts}`) (AC #1, #5, #6)
  - [x] Subtask 2.1: Standalone component, `A11yModule` import + `cdkTrapFocus` on the banner root while open (same pattern as `CartDrawerComponent`/`cart-drawer.component.html` line 12) — Tab must not escape to the rest of the page while the banner is showing (AC #5).
  - [x] Subtask 2.2: Template shows/hides via `@if (consentService.isBannerOpen())` (or equivalent signal), fixed-position banner (e.g. `fixed bottom-0 inset-x-0`), three buttons: "Accepter tout" → `consentService.acceptAll()`, "Refuser" → `consentService.reject()`, "Personnaliser" → expands an inline panel (see 2.3). Every button gets an explicit `aria-label` (AC #6) even though the visible text is already descriptive — mirrors the existing convention in `cart-drawer.component.html` where every interactive control carries both visible text/icon AND `[attr.aria-label]`.
  - [x] Subtask 2.3: "Personnaliser" reveals a single toggle for "Cookies non-essentiels (analytics, marketing)" plus a confirm button ("Enregistrer mes choix") that calls `consentService.acceptCustom(toggleState)`. The epic's AC only distinguishes essential vs. non-essential (no separate analytics/marketing categories are defined anywhere in the PRD/epics), so one toggle is the correct scope — do not invent additional cookie categories.
  - [x] Subtask 2.4: Escape key closes the "Personnaliser" panel back to the 3-button view if it is open; if the main banner (no panel expanded) has focus, Escape has no defined consent-setting behavior in the AC — do NOT interpret Escape as an implicit "Refuser" or "Accepter tout" (that would silently set consent without an explicit choice, defeating the RGPD requirement of explicit consent). Implement Escape as: close the Personnaliser panel if open, else no-op on the banner itself.
  - [x] Subtask 2.5: Use existing design tokens only — `bg-bg-secondary`, `border-border`, `text-text`/`text-text-secondary`, `bg-accent`/`hover:bg-accent-hover` for the primary "Accepter tout" button, `rounded-button`, `focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` on every interactive element (same focus-ring convention as `footer.component.html` and `cart-drawer.component.html`). No new tokens.

- [x] Task 3: Wire the banner globally (AC #1)
  - [x] Subtask 3.1: Add `<app-cookie-banner />` to `app.component.html` alongside `<app-toast />`/`<app-cart-drawer />`, import `CookieBannerComponent` in `app.component.ts`'s `imports` array (same pattern as the other root-level components).

- [x] Task 4: Footer "Modifier mes préférences" link (AC #7)
  - [x] Subtask 4.1: Add a `<button>` (not a `routerLink` — there is no `/preferences-cookies` route, this reopens the existing banner in place) to `footer.component.html`, calling `consentService.reopen()`. Inject `ConsentService` into `FooterComponent`. Style consistent with the existing legal links (`text-text-secondary hover:text-accent underline-offset-4 hover:underline focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 rounded-button`).
  - [x] Subtask 4.2: Update `footer.component.spec.ts` if needed so the existing "renders links to all three legal pages" assertion still passes (it only checks `<a>` `href`s, so a `<button>` addition shouldn't break it — verify after implementing) and add a new test asserting the "Modifier mes préférences" control exists and calls `reopen()` on click.

- [x] Task 5: Unit tests (AC #1–#7)
  - [x] Subtask 5.1: `consent.service.spec.ts` — no stored consent → banner open; `acceptAll()`/`reject()`/`acceptCustom()` persist correct `ConsentRecord` shape to `localStorage` and close the banner; expired (>12 months old) stored consent is treated as absent; `hasNonEssentialConsent()` reflects `reject()` → `false`, `acceptAll()` → `true`.
  - [x] Subtask 5.2: `cookie-banner.component.spec.ts` — renders three buttons with `aria-label`s on first load (no consent); clicking "Refuser" closes the banner and stores `status: 'rejected'`; "Personnaliser" reveals the toggle panel; Escape closes the panel without writing any consent.
  - [x] Subtask 5.3: `footer.component.spec.ts` — "Modifier mes préférences" present and triggers `ConsentService.reopen()`.

### Review Findings

**Patches (fixed during review):**

- [x] [Review][Patch] "Personnaliser" toggle didn't seed from the visitor's current consent, silently downgrading it on save without touching the checkbox [cookie-banner.component.ts] — fixed: `openCustomize()` now seeds `nonEssentialToggle` from `consentService.hasNonEssentialConsent()`.
- [x] [Review][Patch] `localStorage.getItem`/`setItem` not guarded — SecurityError/QuotaExceededError (private browsing, disabled/full storage) could throw during DI construction or a click handler [consent.service.ts] — fixed: both wrapped in try/catch, read failure treated as no consent, write failure swallowed (session-scoped consent still applies via the signal).
- [x] [Review][Patch] Parsed consent record wasn't shape-validated — a wrong-shaped-but-parseable value (e.g. `{}`) produced `Date.now() - undefined = NaN`, and `NaN > TTL` is `false`, so malformed data was silently accepted as valid non-expired consent [consent.service.ts] — fixed: added `isConsentRecord()` shape guard before trusting a parsed record.
- [x] [Review][Patch] Focus wasn't restored to the triggering element when the banner closed (WCAG dialog convention already established in `CartDrawerComponent`) [cookie-banner.component.ts] — fixed: capture/restore `previouslyFocusedElement`, mirroring `CartDrawerComponent`'s pattern.
- [x] [Review][Patch] "Accepter tout" vs "Refuser" had unequal visual weight (filled+bold vs. plain bordered) — a known CNIL dark-pattern concern; valid consent requires equal prominence for accept/reject [cookie-banner.component.html] — fixed: "Refuser" now matches "Accepter tout" in size/weight (outlined vs. filled), "Personnaliser" stays visually secondary as a configuration action, not a decision.

**Deferred (real, not blocking, out of this story's scope):**

- [x] [Review][Defer] No cross-tab consent synchronization (`storage` event listener) [consent.service.ts] — deferred, low practical impact for a banner shown once per session; revisit if multi-tab consent drift is reported.
- [x] [Review][Defer] `aria-modal="true"` without `inert`/`aria-hidden` on background content — `cdkTrapFocus` only intercepts Tab-cycling, doesn't make siblings inert [cookie-banner.component.html] — deferred, same pre-existing gap pattern as `CartDrawerComponent`; full modal semantics belong to Epic 8's dedicated accessibility stories (8.4/8.5).
- [x] [Review][Defer] No `aria-describedby` linking the dialog to its descriptive paragraph [cookie-banner.component.html] — deferred, minor a11y polish; Stories 8.4/8.5 own WCAG work for this epic.
- [x] [Review][Defer] Fixed-position banner has no explicit z-index/scroll-padding coordination with other fixed elements (`app-toast`, `app-cart-drawer`) — deferred, low practical risk (banner is dismissed on first interaction), no reported conflict.
- [x] [Review][Defer] Focus-management `effect()`'s `setTimeout` has no cleanup if `isBannerOpen()` toggles rapidly — deferred, no user-triggered path in this story produces rapid toggling today.

**Dismissed as noise / already addressed / out of scope:**

- "CONSENT_TTL_MS is 13 months per the spec" — false positive; the TTL is 12 months (365 days) and the test correctly exercises the >12-month-expired case using a 13-month-old fixture (a value past the threshold, as a boundary test should use).
- "Non-essential consent isn't granular by purpose" — already an explicit, documented Dev Notes decision: the PRD/epics only define essential vs. non-essential, no separate analytics/marketing categories exist to make granular.
- "Nothing gates actual script loading on consent" — already an explicit, documented Dev Notes decision: no analytics/marketing script exists anywhere in this codebase yet; this story ships the consent gate infrastructure only.
- "No storage-key schema versioning" — speculative future-proofing not required by any AC.
- "No link to `/confidentialite` from within the banner text" — nice-to-have UX, not required by any AC.
- "`footer.component.ts` diff hunk internally inconsistent" — artifact of the reconstructed diff hunk given to the blind reviewer, not a real issue in the actual file (verified: file is valid, `ng build`/`ng test` both pass).
- "AC #7 says 'link', implementation is a `<button>`" — already an intentional, explicitly-documented deviation in the story's own Task 4.1 (no dedicated route exists to link to; the Auditor itself called this "defensible").

## Dev Notes

### Signal-based service + root-level component is this codebase's established pattern for global overlays

`ToastService`/`ToastComponent` and `CartStore`/`CartDrawerComponent` are both: an injectable `providedIn: 'root'` service holding a `signal` of UI state, a standalone component reading that signal via `@if`, and the component mounted once in `app.component.html` (not per-route). `ConsentService`/`CookieBannerComponent` must follow the exact same shape — no `NgRx`, no new state pattern. [Source: `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts`, `frontend/mon-ecommerce-web/src/app/core/components/cart-drawer/cart-drawer.component.ts`]

### SSR guard is mandatory for every `localStorage` touch

This app runs Angular SSR (`server.ts`) — `localStorage`/`document` do not exist server-side. Every existing `localStorage` read/write in this codebase (`cartSessionInterceptor`, `CartDrawerComponent`'s focus effect) is guarded with `inject(PLATFORM_ID)` + `isPlatformBrowser(...)`. `ConsentService` must do the same, both in its constructor (reading stored consent) and in every write method — otherwise SSR will throw and break every page render, not just this feature. [Source: `frontend/mon-ecommerce-web/src/app/core/interceptors/cart-session.interceptor.ts:28-35`]

### No existing analytics/tracking script to retrofit

Confirmed via full-codebase search: this app has zero `gtag`/Google Analytics/marketing scripts anywhere today. AC #2 and #4 ("no analytics or marketing scripts are loaded" / "no tracking scripts load before consent") are about building the **consent gate infrastructure** (`ConsentService.hasNonEssentialConsent()`) for scripts that will be added in a future story — not about modifying an existing integration. Do not add a placeholder analytics script "to demonstrate the gate" — that would be unrequested scope. The service's gate method existing and being correct is what satisfies these ACs for this story.

### 12-month persistence implies expiry, not permanent storage

AC #3 says consent is "persisted for 12 months" — this is a TTL, not a one-time write. Store a `timestamp` in the `ConsentRecord` and treat consent older than 12 months as absent (banner re-shows). Follow the same computed-expiry approach already used for JWT handling in `auth.store.ts` if useful as a reference for date-math conventions, though this is simpler (no server round-trip needed).

### `@angular/cdk` is already a dependency — reuse `cdkTrapFocus`, do not add a new focus-trap library

`^19.2.19` is already in `package.json` and already used via `A11yModule`/`cdkTrapFocus` in `CartDrawerComponent`. Story 8.4 (WCAG forms/navigation, still backlog) will later use CDK's `FocusTrap`/`LiveAnnouncer` more broadly — this story only needs `cdkTrapFocus` on the banner root, same minimal usage as the cart drawer.

### "Personnaliser" scope is intentionally minimal — one toggle, not a category matrix

The epics/PRD only ever distinguish "cookies strictement nécessaires" vs. "cookies non-essentiels" (FR39, Story 8.2 AC) — no separate analytics/marketing/preferences categories are defined anywhere in the planning artifacts. Do not build a multi-category consent matrix; one non-essential toggle satisfies every AC as written.

### Escape key: do not silently set consent

AC #5 requires Escape to be part of the keyboard-accessible interaction, but no AC states what consent Escape should record. Recording an implicit choice on Escape (e.g., treating it as "Refuser") would set consent without the visitor having clicked a labeled button, which undermines the explicit-consent point of the whole feature. Scope Escape's behavior to closing the "Personnaliser" sub-panel only (returning to the 3-button view); it must never itself write a `ConsentRecord`.

### Project Structure Notes

New:
- `frontend/mon-ecommerce-web/src/app/core/services/consent.service.{ts,spec.ts}`
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.{ts,html,scss,spec.ts}`

Modified:
- `frontend/mon-ecommerce-web/src/app/core/constants/storage-keys.ts` (+ `CONSENT_KEY`)
- `frontend/mon-ecommerce-web/src/app/app.component.ts` / `app.component.html` (mount `<app-cookie-banner />`)
- `frontend/mon-ecommerce-web/src/app/core/components/footer/footer.component.{ts,html,spec.ts}` ("Modifier mes préférences")

No backend changes — this story is 100% client-side (consent lives in `localStorage`, never sent to the API). No new routes.

Follows directly from Story 8.1 (footer/legal pages, same Epic 8) — the footer component this story extends was created there.

### References

- `_bmad-output/planning-artifacts/epics.md` — Story 8.2 acceptance criteria (Epic 8 section, line ~1294).
- `_bmad-output/planning-artifacts/prd.md:328` — FR39: cookie consent banner requirement.
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts` — signal-based global-state service pattern.
- `frontend/mon-ecommerce-web/src/app/core/components/cart-drawer/cart-drawer.component.ts` — `cdkTrapFocus`, `PLATFORM_ID`/`isPlatformBrowser` SSR guard, focus-management pattern.
- `frontend/mon-ecommerce-web/src/app/core/interceptors/cart-session.interceptor.ts` — `localStorage` + SSR-guard reference pattern.
- `frontend/mon-ecommerce-web/src/app/core/constants/storage-keys.ts` — existing storage key constants to extend.
- `frontend/mon-ecommerce-web/src/app/core/components/footer/footer.component.{ts,html}` — footer this story extends (built in Story 8.1).
- `frontend/mon-ecommerce-web/src/styles.scss` — Élégance Naturelle design tokens (`--color-accent`, `--radius-button`, etc.) to reuse, no new tokens.

## Dev Agent Record

### Agent Model Used

Claude Opus 5

### Debug Log References

- Full Angular unit test suite run (`ng test --watch=false --browsers=ChromeHeadless`): 171/171 passing, including the 17 new/changed specs (8 `ConsentService`, 8 `CookieBannerComponent`, 1 new footer spec) plus 1 updated `app.component.spec.ts` assertion — up from 154 passing at the end of Story 8.1.
- `ng build` succeeds cleanly; SSR/prerender step still reports "Prerendered 16 static routes" (unchanged from Story 8.1) — confirms the new global `<app-cookie-banner />` mount point doesn't break prerendering (its `ConsentService` constructor SSR-guards every `localStorage` touch, so it renders inert server-side, same as `CartDrawerComponent`).

### Completion Notes List

- `ConsentService` (`core/services/consent.service.ts`) holds consent as a `signal<ConsentRecord | null>` plus a separate `isBannerOpen` signal; reads/writes `localStorage` under the new `CONSENT_KEY`, guarded by `isPlatformBrowser` everywhere (AC #3, #4).
- Consent expiry: a stored record older than 12 months (365 days) is treated as absent, re-opening the banner (AC #3).
- `hasNonEssentialConsent()` computed signal is the gate for any future analytics/marketing script loader (AC #2, #4) — no such script exists anywhere in this codebase today, confirmed via full-tree search; this story ships the gate infrastructure only, not a placeholder integration.
- `CookieBannerComponent` (AC #1, #5, #6): fixed bottom banner, `cdkTrapFocus` while open, three primary actions ("Accepter tout" / "Refuser" / "Personnaliser") each with an explicit `aria-label`, "Personnaliser" reveals a single non-essential-cookies toggle + "Enregistrer mes choix". Escape closes the toggle panel only — it never itself records a consent choice (documented decision, see story Dev Notes "Escape key: do not silently set consent").
- Mounted once in `app.component.html` alongside `<app-toast />`/`<app-cart-drawer />` (AC #1 — shown on every page).
- Footer gained a "Modifier mes préférences" `<button>` (not a route link) calling `ConsentService.reopen()`, which re-shows the banner without clearing the existing stored consent (AC #7).
- `ng test` 171/171 passing (154 pre-existing + 17 new/changed); `ng build` succeeds with prerendering unaffected.
- Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor, run adversarially/independently) found 1 real cross-layer bug (stale "Personnaliser" toggle silently downgrading consent) plus 4 more legitimate patches (storage-failure guards, malformed-record shape validation, focus restoration, accept/reject visual parity) — all fixed, with 6 new regression tests added (2 component, 4 service). 5 lower-impact items deferred to `deferred-work.md` / future accessibility stories (8.4/8.5); 7 items dismissed as noise or already-addressed-in-Dev-Notes. Final: `ng test` 177/177 passing.

### File List

**New:**
- `frontend/mon-ecommerce-web/src/app/core/services/consent.service.ts`
- `frontend/mon-ecommerce-web/src/app/core/services/consent.service.spec.ts`
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.ts`
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.html`
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.scss`
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.spec.ts`

**Modified:**
- `frontend/mon-ecommerce-web/src/app/core/constants/storage-keys.ts` (+ `CONSENT_KEY`)
- `frontend/mon-ecommerce-web/src/app/app.component.ts` / `app.component.html` (`<app-cookie-banner />`)
- `frontend/mon-ecommerce-web/src/app/app.component.spec.ts` (+ mount-point assertion)
- `frontend/mon-ecommerce-web/src/app/core/components/footer/footer.component.ts` / `.html` / `.spec.ts` ("Modifier mes préférences")
