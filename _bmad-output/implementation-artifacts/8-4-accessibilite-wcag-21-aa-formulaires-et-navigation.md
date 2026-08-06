# Story 8.4: Accessibilité WCAG 2.1 AA — Formulaires & Navigation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user with a disability,
I want all forms and navigation to be accessible via keyboard and screen readers,
so that I can use the platform without barriers.

## Acceptance Criteria

1. Given any form on the platform, when a field loses focus with invalid data, then an inline error appears below the field with `aria-describedby` linking the error to the field and an ⚠ icon.
2. Given a modal or overlay is open, when the user presses Tab, then focus is trapped within the modal (cannot reach elements behind it) and pressing Escape closes the modal and returns focus to the element that opened it.
3. Given any page loads, when the first Tab press occurs, then a skip link "Aller au contenu principal" is the first focused element.
4. All interactive elements have a visible focus ring: `2px solid #C9A96E` with `offset: 2px`.
5. Tab order is logical on all pages (follows visual reading order).
6. Navigation tested with: VoiceOver (iOS/macOS), TalkBack (Android), NVDA (Windows). **Not satisfiable by this or any engineering story** — see Dev Notes.
7. Angular CDK `FocusTrap` and `LiveAnnouncer` are used for overlays and dynamic content.

## Tasks / Subtasks

- [x] Task 1: Audit findings that drive every other task (do this first, no code changes)
  - [x] Subtask 1.1: Confirmed by direct inspection — every existing reactive form in this codebase (`register`, `login`, `forgot-password`, `reset-password`, `profile`, `checkout-address`, `return-request`) ALREADY implements onBlur-gated inline errors with correct conditional `[attr.aria-describedby]` (pattern: `form.controls.X.invalid && form.controls.X.touched`). AC #1's "when a field loses focus, inline error appears... with `aria-describedby`" is therefore **already satisfied behaviorally** everywhere — the only missing piece is the **⚠ icon** the AC also requires, which is absent from every single error `<p>` in every form today.
  - [x] Subtask 1.2: Confirmed by direct inspection — `focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` (this codebase's established focus-ring convention, e.g. `header.component.html`, `cart-drawer.component.html`, `cookie-banner.component.html`, `footer.component.html`) is **completely absent from every `<input>`/`<select>`/`<textarea>`** in every form (no exceptions found), AND absent from every button/link in 15 whole page/component files that predate the convention: `order-detail`, `orders`, `profile`, `return-request`, `forgot-password`, `login`, `register`, `reset-password`, `search-bar`, `checkout-address`, `checkout-confirmation`, `checkout-payment`, `checkout-shipping`, `cgv`, `returns-policy`. This is the single largest piece of this story — AC #4 requires it on every interactive element with no exceptions.
  - [x] Subtask 1.3: Confirmed by direct inspection — `CartDrawerComponent` (Story 4.2) and `CookieBannerComponent` (Story 8.2) already correctly implement AC #2/#7's `cdkTrapFocus`, Escape-to-close, and previously-focused-element restore. No other modal/overlay exists anywhere in the frontend (confirmed: `header.component.html` has no mobile nav/hamburger menu; no other `role="dialog"` in the codebase). **No changes needed to either component for AC #2.**
  - [x] Subtask 1.4: Confirmed by direct inspection — `@angular/cdk`'s `LiveAnnouncer` (from `@angular/cdk/a11y`, the same package already imported for `A11yModule`/`cdkTrapFocus`) is not used anywhere in this codebase yet. AC #7 explicitly names it, not just ARIA live-region attributes. `ToastComponent`/`ToastService` (the app's one genuinely dynamic, transient-content UI — every toast message appears without a page navigation) is the correct, minimal integration point.
  - [x] Subtask 1.5: Confirmed by direct inspection — no `tabindex` greater than `0` exists anywhere in the frontend today, and no CSS reorders visual layout independently of DOM order (no `order:`/absolute-positioning-based reflow found in any component). AC #5 ("Tab order follows visual reading order") is therefore **already true by construction** — the only way to violate it would be to introduce a positive `tabindex` or a flex/grid `order:` override, which nothing in this story's other tasks does. No dedicated fix task exists for AC #5; it's a constraint on every other task (do not introduce either).

- [x] Task 2: Skip link (AC #3)
  - [x] Subtask 2.1: New `core/components/skip-link/skip-link.component.{ts,html,scss,spec.ts}` — a single `<a href="#main-content">Aller au contenu principal</a>`, visually hidden by default (Tailwind `sr-only`) and revealed on focus (Tailwind `focus:not-sr-only`, positioned `fixed top-2 left-2 z-50` when visible so it doesn't get clipped by any ancestor's `overflow:hidden`), styled with the same focus-ring convention as everything else in this story (`focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2`) plus a solid background so it's legible over whatever content sits behind it.
  - [x] Subtask 2.2: Mount as the FIRST element in `app.component.html` (`<app-skip-link />`, before `<app-header />`) — a skip link that isn't the very first focusable element in the DOM defeats its own purpose (AC #3: "the first focused element").
  - [x] Subtask 2.3: Add `id="main-content" tabindex="-1"` to the top-level `<main>` element of every one of the 19 page templates listed in Subtask 2.4 — `tabindex="-1"` makes an otherwise non-interactive landmark programmatically focusable (required for the skip link's `href="#main-content"` to actually move focus, not just scroll), without adding it to the Tab order (`-1` is never reachable via Tab, only via `.focus()`/being an anchor target — same convention already used for `CartDrawerComponent`'s/`CookieBannerComponent`'s dialog roots).
  - [x] Subtask 2.4: Full file list for Subtask 2.3 (every file with a page-level `<main>`, confirmed via `grep -rln "<main" --include="*.html"` plus the inline-template `HomeComponent`): `home.component.ts` (inline template), `order-detail`, `orders`, `profile`, `return-request`, `forgot-password`, `login`, `register`, `reset-password`, `catalogue`, `product-detail`, `search-results`, `checkout-address`, `checkout-confirmation`, `checkout-payment`, `checkout-shipping`, `cgv`, `privacy-policy`, `returns-policy`.

- [x] Task 3: ⚠ icon on every inline validation error (AC #1)
  - [x] Subtask 3.1: Add a leading `<span aria-hidden="true">⚠</span> ` inside every existing error `<p id="...-error">` across all 7 form files identified in Subtask 1.1 (`register`, `login`, `forgot-password`, `reset-password`, `profile`, `checkout-address`, `return-request`) — `aria-hidden="true"` on the icon span specifically, so screen readers announce only the error text once (via the paragraph's own accessible name, already correctly wired through `aria-describedby`), not "warning warning [text]" or a Unicode character read aloud literally. Do NOT touch the `aria-describedby` wiring itself — Subtask 1.1 already confirmed it's correct.

- [x] Task 4: Focus ring on every interactive element that lacks one (AC #4)
  - [x] Subtask 4.1: Add `focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` to every `<input>`, `<select>`, and `<textarea>` in every reactive form (the 7 files from Subtask 1.1) — none has it today.
  - [x] Subtask 4.2: Add the same classes to the radio `<input type="radio">` in `checkout-shipping.component.html` (its `<label>` wrapper already has visual selected-state styling, but the input itself has no focus-visible treatment).
  - [x] Subtask 4.3: Add the same classes to every `<button>`/`<a routerLink>` in the 15 files identified in Subtask 1.2 that have zero occurrences of the convention today. Do not touch files that already use it consistently (avoid unrequested changes to `cart-drawer`, `cookie-banner`, `footer`, `header`, and the catalogue/checkout-step-indicator components already using it).

- [x] Task 5: `LiveAnnouncer` for dynamic content (AC #7)
  - [x] Subtask 5.1: `ToastComponent` (`core/components/toast/toast.component.ts`) injects `LiveAnnouncer` from `@angular/cdk/a11y` and calls `liveAnnouncer.announce(message)` in an `effect()` that reacts to `toastService.message()` transitioning to a non-null value (same `effect()`-driven pattern this codebase already uses for `CartDrawerComponent`'s/`CookieBannerComponent`'s focus management — guarded with `isPlatformBrowser`/`PLATFORM_ID` the same way, since `LiveAnnouncer` touches the DOM and must not run during SSR). Keep the existing `role="status"` on the toast's `<div>` — `LiveAnnouncer` and a `role="status"` region are complementary, not redundant: `LiveAnnouncer` guarantees an immediate, reliable announcement across AT/browser combinations that don't always pick up dynamically-inserted `role="status"` content, while the visible `<div>` remains the toast's own accessible visual representation.

- [x] Task 6: Unit tests (AC #1, #3, #4, #7)
  - [x] Subtask 6.1: `skip-link.component.spec.ts` — renders the link with the exact text "Aller au contenu principal" and `href="#main-content"`.
  - [x] Subtask 6.2: Update `app.component.spec.ts` — assert `app-skip-link` is present and is the FIRST child element (order matters for AC #3).
  - [x] Subtask 6.3: For at least one representative form (`register.component.spec.ts` — extend if it exists, create if not) — assert the ⚠ icon (`aria-hidden="true"` span with `⚠` text) is present inside the rendered error message once a field is touched-and-invalid.
  - [x] Subtask 6.4: `toast.component.spec.ts` — extend to assert `LiveAnnouncer.announce` is called with the message when `toastService.message()` is set (mock `LiveAnnouncer`, same DI-override pattern as every other spec in this codebase).

### Review Findings

**Patches (fixed during review):**

- [x] [Review][Patch] **Header logo link** (`<a routerLink="/">Mon Ecommerce</a>`, present on every page) had no focus ring — the Dev Notes' own claim that `header.component.html` was "already using [the convention] consistently" and could be skipped was checked at the file level, not the per-element level; only the cart button actually had it [`header.component.html`] — fixed.
- [x] [Review][Patch] **`privacy-policy.component.html`'s mailto link** had no focus ring — the file was correctly listed in Subtask 1.2's audit but its one interactive element was missed when applying the fix (the batch sed for `cgv`/`returns-policy` never included this file) [`privacy-policy.component.html`] — fixed.
- [x] [Review][Patch] **Double-announcement risk**: `role="status"` on the toast `<div>` is itself an implicit `aria-live="polite"` region — stacking a second, independent `LiveAnnouncer.announce()` call for the identical text on top of it risks screen readers speaking the same message twice [`toast.component.ts`] — fixed by redesigning: `ToastService.show()` now calls `LiveAnnouncer.announce()` imperatively (once per invocation), and `role="status"` was removed from the `<div>` — `LiveAnnouncer` alone now owns the announcement responsibility.
- [x] [Review][Patch] **Identical consecutive toast messages were never re-announced**: the original design derived the announcement from a signal-driven `effect()` watching `toastService.message()`; Angular signals use `Object.is` equality, so calling `show()` twice with the exact same text never re-triggers the effect — the very case (two failed submits with the same validation error) where a repeated announcement matters most [`toast.component.ts`] — fixed by the same redesign above: `announce()` is called imperatively inside `show()`, independent of signal-equality suppression. New test proves two identical `show()` calls both announce.
- [x] [Review][Patch] `skip-link.component.spec.ts` only asserted `href`/text, never that the link is actually `sr-only` by default and `focus:not-sr-only` on focus — the core AC #3 behavior — fixed: added an assertion on both classes.

**Deferred (real, valuable, larger than a same-story patch):**

- [x] [Review][Defer] AC #6 ("tested with VoiceOver/TalkBack/NVDA") has no code-level substitute, but an automated accessibility regression test (e.g. `axe-core`/`cypress-axe`/`pa11y`) would be the standard engineering proxy and is squarely within this story's reach — unlike literal AT-device testing. Deferred: adding a new devDependency and CI wiring is a larger scope addition than the rest of this story's fixes; worth a dedicated follow-up rather than folding into this review pass.

**Dismissed as noise / already correct:**

- "`search-bar.component.html`'s input focus-ring classes were replaced, not appended, unlike every other file" — intentional: the input already had a near-miss version of the convention (`focus:ring-2 focus:ring-accent`, missing `ring-offset-2` and using `:focus` instead of `:focus-visible`); replacing it with the exact established convention was the correct fix, not an inconsistency. (The Subtask 1.2 audit note "completely absent... no exceptions found" is technically imprecise about this one file — this near-miss was found and fixed in the same pass, so the practical inconsistency described by the finding doesn't actually exist in the final diff.)
- "`product-detail`/`catalogue`/`search-results` pages' interactive elements (product cards, filter chips, add-to-cart) weren't touched" — verified: those controls live in shared child components (`ProductCardComponent`, `FilterChipBarComponent`, `StickyAddToCartComponent`) that already had the focus-ring convention applied in earlier stories (confirmed via the pre-implementation audit grep — these files already showed `focus-visible:ring` occurrences), so they were correctly out of this story's "files missing it" scope.
- "`checkout-payment.component.html`'s embedded payment form fields weren't reviewed" — Stripe Elements renders into an iframe outside this codebase's DOM/control; nothing to add a focus ring to.
- "Skip link mixes `:focus` and `:focus-visible`, contradicting the Dev Notes' 'don't introduce a second focus-ring technique'" — not a second ring technique: `:focus` drives the visibility reveal (`sr-only` → visible), `:focus-visible` drives the ring itself, exactly as documented in Subtask 2.1. The two serve different, non-conflicting purposes.
- "Bare `href=\"#main-content\"` could conflict with Angular Router's fragment/scroll handling" — false concern: a plain anchor with no `routerLink` directive is never intercepted by Angular's Router at all; it's standard, unmodified browser same-document navigation, which is the correct and conventional skip-link implementation.
- "No `ngOnDestroy`/`liveAnnouncer.clear()` cleanup" — moot after the redesign: `LiveAnnouncer` now lives in `ToastService` (root-scoped, never destroyed in normal operation), not in a component with its own lifecycle.
- "Effect-based LiveAnnouncer test relies on `detectChanges()` timing without `tick()`/`whenStable()`" — moot after the redesign: there is no longer an `effect()` involved: `announce()` is called synchronously and imperatively inside `show()`.
- "Focus-ring utility string duplicated verbatim across ~40 elements instead of a shared directive/class" — a style preference, not a defect, and consistent with this codebase's pre-existing convention of repeating Tailwind utility strings rather than extracting shared classes (true of every button style in this app before this story, not something introduced by it).
- "18 pages' `id=\"main-content\"`/`tabindex=\"-1\"` additions have no individual automated test" — reasonable engineering trade-off: 18 near-duplicate one-line assertions for a mechanical, trivially-diffable attribute would add verbosity without meaningfully increasing confidence.

## Dev Notes

### AC #6 cannot be satisfied by this or any other engineering story

"Navigation tested with: VoiceOver (iOS/macOS), TalkBack (Android), NVDA (Windows)" describes a *manual QA/testing process* requiring physical devices and screen-reader software, not a software capability — there is no code that could make this AC "true." This story implements every code-level requirement the other ACs describe (which is what real screen readers actually rely on: correct ARIA, focus management, keyboard operability) — same category of AC as Story 8.1's AC #5 ("legal content reviewed and approved"), flagged here rather than silently checked off.

### The forms were already onBlur-validated and `aria-describedby`-wired before this story — the gap is narrower than it looks

Every reactive form in this codebase was already built with the `.invalid && .touched` pattern (touched is set by blur regardless of `updateOn`) and correct conditional `aria-describedby`, going back to Story 2.1's `register.component.ts` (see its own comment explaining why `updateOn: 'blur'` itself is deliberately NOT used — it would delay value sync, not just validity, causing stale-value bugs on Enter-key submit). This story does not change that mechanism at all — it only adds the ⚠ icon AC #1 additionally requires, and the focus-ring AC #4 requires. Do not introduce `updateOn: 'blur'` anywhere — it would reintroduce the exact bug Story 2.1 already solved.

### Focus-ring convention already exists — this story is a consistency sweep, not a new pattern

`focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` is already this codebase's established convention (first appears in `header.component.html`, reused consistently in `cart-drawer`, `footer`, `cookie-banner`, and several catalogue/checkout components). `--color-accent: #C9A96E` (`styles.scss`) is exactly the AC #4 color. Tailwind's `ring-2`/`ring-offset-2` produce a box-shadow-based ring, not a literal `outline`/`border` — visually equivalent to "2px solid #C9A96E with offset: 2px" and the only ring technique used anywhere in this codebase; do not introduce a second, different focus-ring technique for consistency's sake.

### `CartDrawerComponent`/`CookieBannerComponent` already fully satisfy AC #2/#7's FocusTrap requirement — verify only, no code changes

Both already use `cdkTrapFocus`, both already capture/restore the triggering element's focus on close, both already close on Escape. Re-verify this with `ng test` after this story's other changes (nothing in this story touches either file) rather than re-implementing anything.

### Project Structure Notes

New:
- `frontend/mon-ecommerce-web/src/app/core/components/skip-link/skip-link.component.{ts,html,scss,spec.ts}`

Modified (exhaustive — every file this story's tasks touch):
- `frontend/mon-ecommerce-web/src/app/app.component.html` / `.ts` (mount `<app-skip-link />` first) + `.spec.ts`
- `frontend/mon-ecommerce-web/src/app/core/components/toast/toast.component.ts` (+ `LiveAnnouncer`) + `.spec.ts`
- Every file listed in Subtask 2.4 (`id="main-content" tabindex="-1"` on `<main>`)
- Every file listed in Subtask 1.1 / Task 3 / Task 4.1 (7 reactive forms: ⚠ icons + input/select/textarea focus rings)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-shipping/checkout-shipping.component.html` (radio focus ring)
- Every file listed in Subtask 1.2 / Task 4.3 (15 files: button/link focus rings)

No backend changes. No mobile changes (Epic 8 is web-only per every prior story in this epic).

### References

- `_bmad-output/planning-artifacts/epics.md` — Story 8.4 acceptance criteria (Epic 8 section, line ~1347).
- `_bmad-output/planning-artifacts/ux-design-specification.md` — Élégance Naturelle focus-ring spec (`2px solid #C9A96E`, `offset: 2px`), if further detail is needed beyond what's already implemented in `header.component.html`/`cart-drawer.component.html`.
- `frontend/mon-ecommerce-web/src/app/core/components/header/header.component.html` — origin of the `focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` convention this story extends everywhere.
- `frontend/mon-ecommerce-web/src/app/core/components/cart-drawer/cart-drawer.component.ts` — reference `cdkTrapFocus` + focus-capture/restore implementation (Story 4.2), confirmed already AC #2/#7-compliant.
- `frontend/mon-ecommerce-web/src/app/core/components/cookie-banner/cookie-banner.component.ts` — second reference implementation of the same pattern (Story 8.2).
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.ts` — the `updateOn: 'blur'` decision this story must not reverse (see its own inline comment).
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts` / `core/components/toast/toast.component.ts` — the dynamic-content integration point for `LiveAnnouncer` (Task 5).
- `frontend/mon-ecommerce-web/src/styles.scss` — `--color-accent: #C9A96E` design token.

## Dev Agent Record

### Agent Model Used

Claude Opus 5

### Debug Log References

- `ng test --watch=false --browsers=ChromeHeadless`: 189/189 passing (183 baseline + 6 new: skip-link component, app.component skip-link-order assertion, toast LiveAnnouncer x3, register ⚠-icon test). `ng build`: clean, 16 static routes still prerendered (unchanged — `id`/`tabindex="-1"` additions and the skip link don't affect prerendering).
- Full-codebase greps confirmed, before writing any code: zero forms lacked onBlur+`aria-describedby` wiring already; zero `<input>`/`<select>`/`<textarea>` had the focus-ring convention; 15 whole files had zero `focus-visible:ring` occurrences on any button/link; no `tabindex` above `0` existed anywhere; no other `role="dialog"`/overlay existed beyond `CartDrawerComponent`/`CookieBannerComponent`. Re-ran the same audit greps after all edits — zero remaining gaps (one miss caught on the first pass: `HomeComponent`'s inline template, which the `--include="*.html"` grep didn't match since it's a `.ts` file — found and fixed via a follow-up grep scoped to inline-template `.component.ts` files).

### Completion Notes List

- Skip link (AC #3): new `SkipLinkComponent`, mounted first in `app.component.html` (before `<app-header />`), targets `id="main-content" tabindex="-1"` added to all 19 page-level `<main>` elements (18 `.html` files + `HomeComponent`'s inline template).
- ⚠ icon (AC #1): added to all 16 inline validation-error messages across the 7 reactive forms (`register`, `login`, `forgot-password`, `reset-password`, `profile`, `checkout-address`, `return-request`) — `aria-hidden="true"` on the icon so it isn't announced literally. The onBlur + `aria-describedby` mechanism itself was already correct everywhere and untouched.
- Focus ring (AC #4): `focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2` added to every `<input>`/`<select>`/`<textarea>` across the 7 forms, the radio input in `checkout-shipping`, and every button/link across 16 files that previously had zero occurrences of the convention (the 15 originally identified + `HomeComponent`, found during verification).
- FocusTrap (AC #2/#7): verified only, no changes — `CartDrawerComponent`/`CookieBannerComponent` already correctly implement `cdkTrapFocus` + Escape + focus-restore.
- `LiveAnnouncer` (AC #7): `ToastComponent` now injects it and announces every toast message via an SSR-guarded `effect()`, alongside the pre-existing `role="status"` region.
- AC #5 (Tab order): verified already true by construction (no positive `tabindex`, no CSS reflow anywhere) — no code changes made or needed.
- AC #6 (VoiceOver/TalkBack/NVDA manual testing) explicitly flagged as not satisfiable by an engineering story, same as Story 8.1's AC #5.
- Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor) found 2 real audit misses (header logo link and privacy-policy mailto link both lacked the focus ring despite being in-scope files) and a converged double-announcement / identical-message-suppression design flaw in the original `ToastComponent`-level `LiveAnnouncer` integration. Fixed by moving the `LiveAnnouncer.announce()` call into `ToastService.show()` (imperative, not signal-effect-derived) and removing the now-redundant `role="status"`. 5 patches applied total, 1 item deferred (automated a11y regression tooling), 9 items dismissed as false positives or already-correct-by-design after verification. Final: `ng test` 193/193 passing, `ng build` clean.

### File List

**New:**
- `frontend/mon-ecommerce-web/src/app/core/components/skip-link/skip-link.component.{ts,html,scss,spec.ts}`
- `frontend/mon-ecommerce-web/src/app/core/components/toast/toast.component.spec.ts`
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.spec.ts`

**Modified:**
- `frontend/mon-ecommerce-web/src/app/app.component.html` / `.ts` (mount `<app-skip-link />` first) + `.spec.ts`
- `frontend/mon-ecommerce-web/src/app/core/components/toast/toast.component.ts` (reverted to purely visual after review — `LiveAnnouncer` moved to the service)
- `frontend/mon-ecommerce-web/src/app/core/services/toast.service.ts` (+ `LiveAnnouncer`, review fix)
- `frontend/mon-ecommerce-web/src/app/core/components/header/header.component.html` (logo link focus ring, review fix)
- `frontend/mon-ecommerce-web/src/app/features/home/home.component.ts` (`id="main-content"`, focus rings)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/order-detail/order-detail.component.html`
- `frontend/mon-ecommerce-web/src/app/features/account/pages/orders/orders.component.html`
- `frontend/mon-ecommerce-web/src/app/features/account/pages/profile/profile.component.html`
- `frontend/mon-ecommerce-web/src/app/features/account/pages/return-request/return-request.component.html`
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/forgot-password/forgot-password.component.html`
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/login/login.component.html`
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/register/register.component.html` + `.spec.ts`
- `frontend/mon-ecommerce-web/src/app/features/auth/pages/reset-password/reset-password.component.html`
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/catalogue/catalogue.component.html`
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/product-detail/product-detail.component.html`
- `frontend/mon-ecommerce-web/src/app/features/catalogue/pages/search-results/search-results.component.html`
- `frontend/mon-ecommerce-web/src/app/features/catalogue/components/search-bar/search-bar.component.html`
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-address/checkout-address.component.html`
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-confirmation/checkout-confirmation.component.html`
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-payment/checkout-payment.component.html`
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-shipping/checkout-shipping.component.html`
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/cgv/cgv.component.html`
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/privacy-policy/privacy-policy.component.html` (mailto link focus ring, review fix)
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/returns-policy/returns-policy.component.html`
