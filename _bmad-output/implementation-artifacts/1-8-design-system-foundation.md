# Story 1.8: Design System Foundation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a frontend/mobile developer,
I want the Tailwind CSS v4 design tokens (Élégance Naturelle palette, typography scale, spacing, breakpoints) and Flutter `design_tokens.dart` configured,
so that all UI components share consistent visual values across web and mobile.

## Acceptance Criteria

1. **Given** the Angular app is running, **when** any component uses Tailwind utility classes, **then** CSS custom properties `--color-accent: #C9A96E`, `--color-text: #111111`, `--color-bg: #FFFFFF`, `--color-bg-secondary: #FAF8F5` (plus the remaining palette tokens below) are available globally.
2. **Given** a heading element uses the configured typography, **when** rendered in the browser, **then** Cormorant Garamond is applied to H1/H2 and DM Sans to body, labels, and buttons.
3. **Given** the Flutter app is running, **when** any widget references `AppTokens.accentColor`, **then** `#C9A96E` is applied consistently.
4. Tailwind breakpoints are configured: `sm:640px` `md:768px` `lg:1024px` `xl:1280px`.
5. The 8px grid spacing scale (4/8/16/24/32/48/64px) is documented in a shared config.
6. Border-radius tokens are set: 4px for cards/inputs, 2px for buttons.

## Tasks / Subtasks

- [x] Task 1: Install and configure Tailwind CSS v4 in the Angular project (AC: #1, #4, #6)
  - [x] From `frontend/mon-ecommerce-web/`, run `ng add tailwindcss` (official Angular schematic — installs `tailwindcss` + `@tailwindcss/postcss` + `postcss`, creates `.postcssrc.json`, and rewrites `src/styles.scss` to use the SCSS-safe `@use 'tailwindcss';` form). If the schematic is unavailable, fall back to the manual steps in Dev Notes.
  - [x] Add an `@theme { ... }` block to `src/styles.scss` (after the `@use 'tailwindcss';` line) declaring the full color palette, font-family tokens, and named border-radius tokens — see Dev Notes for the exact block to paste.
  - [x] Explicitly declare the 4 breakpoints in the same `@theme` block even though they match Tailwind v4's defaults (see Dev Notes rationale) — makes AC #4 self-documenting and future-proof against default changes.
  - [x] Do **not** redefine `--spacing` — Tailwind v4's default spacing scale already produces the required 4/8/16/24/32/48/64px values (see Dev Notes mapping table). Add a one-line comment above `@theme` documenting this mapping to satisfy AC #5 without touching the base scale.
  - [x] `ng build` succeeds with no errors
- [x] Task 2: Load Cormorant Garamond + DM Sans in Angular and apply them globally (AC: #2)
  - [x] Add Google Fonts `<link rel="preconnect">` + stylesheet `<link>` tags to `src/index.html` `<head>` for Cormorant Garamond (weights 400, 500) and DM Sans (weights 400, 500, 600)
  - [x] In `src/styles.scss`, add an `@layer base` rule applying `font-family: var(--font-heading)` to `h1, h2` and `font-family: var(--font-body)` to `body` (so bare HTML elements get the right font before any component markup exists)
  - [x] `ng build` succeeds with no errors
- [x] Task 3: Angular verification test (AC: #1, #2, #4, #6)
  - [x] Create `src/app/design-tokens.spec.ts` (Karma/Jasmine, same pattern as `app.component.spec.ts`) — see Completion Notes for a deviation from the plan (utility-class assertions replaced with direct `var()` assertions after discovering a Tailwind/Karma pipeline quirk)
  - [x] `npm test` passes (7/7)
- [x] Task 4: Create Flutter design tokens and theme (AC: #3, #5, #6)
  - [x] Add `google_fonts` to `pubspec.yaml` dependencies (approved addition for this story — see Dev Notes)
  - [x] Create `lib/app/theme/design_tokens.dart` with an `AppTokens` class: `static const Color` fields for all 9 palette colors, `static const double` spacing constants (`space4`...`space64`), and radius constants (`radiusCard = 4`, `radiusButton = 2`)
  - [x] Create `lib/app/theme/app_theme.dart` with `AppTheme.lightTheme` (Material 3 `ThemeData`, `colorScheme` built from `AppTokens`, `textTheme` mapping Cormorant Garamond onto `headlineLarge`/`headlineMedium` and DM Sans onto the rest via `google_fonts`)
  - [x] Update `lib/main.dart`: replaced the inline `colorScheme: .fromSeed(...)` with `theme: AppTheme.lightTheme` — no other restructuring
  - [ ] `flutter analyze` — **not run**, Flutter/Dart SDK unavailable in this environment (see Completion Notes)
- [x] Task 5: Flutter verification test (AC: #3, #5, #6)
  - [x] Create `test/app/theme/app_theme_test.dart` (`flutter_test`) — 4 tests: primary color, H1 font family, body font family, radius constants
  - [ ] `flutter test` — **not run**, Flutter/Dart SDK unavailable in this environment (see Completion Notes)

### Review Findings

- [x] [Review][Patch] Breakpoint tokens (`--breakpoint-sm/md/lg/xl`) were excluded from the plain `:root` duplication applied to colors/fonts/radii — currently masked because the chosen values (40/48/64/80rem) equal Tailwind v4's own defaults, so an empty `@theme` under the Karma pipeline still yields correct breakpoint behavior by coincidence. The moment anyone customizes a breakpoint away from the default, it silently stops working under `ng test` with no fallback, unlike every other token category [frontend/mon-ecommerce-web/src/styles.scss] — fixed: breakpoints added to the `:root` block; `ng build` + `ng test` re-verified (7/7 passing)
- [x] [Review][Patch] `google_fonts` performs real network fetches during Flutter widget tests — `app_theme_test.dart` calls `AppTheme.lightTheme` → `GoogleFonts.dmSansTextTheme(...)`/`cormorantGaramond(...)` with no `GoogleFonts.config.allowRuntimeFetching = false` guard, risking flaky/hanging tests in network-isolated CI [mobile/mon_ecommerce_mobile/test/app/theme/app_theme_test.dart] — fixed: added `setUpAll` disabling runtime fetching (standard `google_fonts` testing pattern); unverified by `flutter test` per the existing Flutter-tooling gap
- [x] [Review][Patch] AC #2 ("DM Sans to body, labels, and buttons") is only enforced explicitly for `body` in Angular — buttons/labels/inputs get DM Sans purely through CSS inheritance from `body`, which is an implicit, untested dependency rather than the explicit rule the AC's wording implies [frontend/mon-ecommerce-web/src/styles.scss] — fixed: `button, input, label` added to the explicit `@layer base` rule; `ng build` + `ng test` re-verified (7/7 passing)
- [x] [Review][Defer] `pubspec.lock` was never regenerated for the new `google_fonts` dependency (confirmed via grep — no entry exists) [mobile/mon_ecommerce_mobile/pubspec.lock] — deferred, blocked by the same pre-existing "Flutter/Dart SDK unavailable in this environment" gap already logged in `deferred-work.md`; requires running `flutter pub get` once tooling is available
- [x] [Review][Defer] Google Fonts loaded directly from `fonts.googleapis.com`/`fonts.gstatic.com` CDN leaks visitor IPs to Google without consent — a known GDPR exposure for an EU/French-facing site [frontend/mon-ecommerce-web/src/index.html] — deferred to Epic 8 ("Conformité, Accessibilité & Qualité"), which already owns RGPD/cookie-consent scope (FR38-40); self-hosting fonts is the standard mitigation and was already flagged as a future revisit in this story's own Dev Notes
- [x] [Review][Defer] No automated check keeps the 3 independent hand-maintained token copies in sync (`@theme` block, plain `:root` block, and Flutter's `AppTokens`) — deferred until Story 1.9 (CI/CD pipeline) exists to host such a cross-language parity check; not practical to build in isolation before CI infrastructure is in place

## Dev Notes

### Current state (verified before writing this story — greenfield, nothing to preserve)

- **Angular:** Tailwind is **not installed** — no `tailwindcss` in `package.json`, no `tailwind.config.*` file. `src/styles.scss` is empty (only the default `ng new` comment). `src/index.html` has no font `<link>` tags. `src/app/app.config.ts` has no theme providers. The whole `src/` tree is still the unmodified `ng new --ssr` scaffold (11 files, zero custom components) — Angular is `^19.2.0`, `@angular/ssr ^19.2.24`, `typescript ~5.7.2`.
- **Flutter:** `lib/` contains only `main.dart`, and it is the **unmodified Flutter counter-app template** (`ColorScheme.fromSeed(seedColor: Colors.deepPurple)`, `MyHomePage` counter demo) — confirmed there is no `lib/app/`, `lib/features/`, or `lib/shared/` yet. `pubspec.yaml` has only `cupertino_icons` as a dependency; the `fonts:`/`assets:` sections are commented-out template placeholders. Dart SDK constraint is `^3.11.4`.
- This means Task 4's `main.dart` edit is the **only** touch to that file — do not add routing, features, or remove the counter demo; that's Epic 3+ scope.

### Tailwind CSS v4 setup — do this, not the v3 way

Tailwind v4 replaced the JS `tailwind.config.js` + `@tailwind base/components/utilities` directives with a CSS-first config: everything (including this story's design tokens) is declared inside a single `@theme { ... }` block in your main stylesheet, and Tailwind auto-generates both the CSS custom property **and** matching utility classes from each `@theme` key.

**Installation — use the official Angular schematic, don't hand-rolled a PostCSS config:**
```bash
cd frontend/mon-ecommerce-web
ng add tailwindcss
```
Per Angular's own docs (angular.dev/guide/tailwind), for a project using `.scss` (this one does), the schematic writes:
```scss
@use 'tailwindcss';
```
into `src/styles.scss` — **use `@use`, not `@import`**. Tailwind v4's own docs recommend plain `.css` for the main import file because Sass's `@import` conflicts with Tailwind's own `@import` syntax; Angular's schematic sidesteps this by using Sass's `@use` directive instead, which is safe. Do not manually write `@import 'tailwindcss';` into a `.scss` file.

If `ng add tailwindcss` is unavailable for any reason, the manual fallback is:
```bash
npm install tailwindcss @tailwindcss/postcss postcss
```
then create `.postcssrc.json` at the frontend project root:
```json
{ "plugins": { "@tailwindcss/postcss": {} } }
```
then add `@use 'tailwindcss';` as the first line of `src/styles.scss`.

**The `@theme` block to add to `src/styles.scss`** (after the `@use` line):
```scss
@theme {
  /* Élégance Naturelle palette — see _bmad-output/planning-artifacts/ux-design-specification.md#Système de Couleurs */
  --color-bg: #FFFFFF;
  --color-bg-secondary: #FAF8F5;
  --color-text: #111111;
  --color-text-secondary: #555555;
  --color-accent: #C9A96E;
  --color-accent-hover: #A8864A;
  --color-border: #E5E5E5;
  --color-success: #6B8F71;
  --color-error: #C0564A;

  /* Typography */
  --font-heading: "Cormorant Garamond", serif;
  --font-body: "DM Sans", sans-serif;

  /* Border-radius (AC #6) */
  --radius-card: 4px;
  --radius-button: 2px;

  /* Breakpoints — match Tailwind v4 defaults exactly; declared explicitly per AC #4 (see rationale below) */
  --breakpoint-sm: 40rem;  /* 640px */
  --breakpoint-md: 48rem;  /* 768px */
  --breakpoint-lg: 64rem;  /* 1024px */
  --breakpoint-xl: 80rem;  /* 1280px */
}
```
This generates utility classes automatically: `bg-accent`, `text-accent`, `border-accent`, `rounded-card`, `rounded-button`, `font-heading`, `font-body`, and the `sm:`/`md:`/`lg:`/`xl:` prefixes — no additional config needed.

**Why explicitly declare breakpoints that already match the defaults:** Tailwind v4's out-of-the-box breakpoints (`sm:40rem`, `md:48rem`, `lg:64rem`, `xl:80rem`, `2xl:96rem`) already equal the UX spec's values, so *functionally* nothing would break if you skipped this. But AC #4 says breakpoints must be "configured," and relying on an implicit default is fragile (a future Tailwind major version could change its defaults silently). Declaring them explicitly in `@theme` costs 4 lines and makes the requirement self-documenting.

**Why NOT to redefine `--spacing`:** Tailwind v4's default spacing scale is generated from a single `--spacing: 0.25rem` base multiplier (4px). This already yields the exact values AC #5 requires: `p-1`=4px, `p-2`=8px, `p-4`=16px, `p-6`=24px, `p-8`=32px, `p-12`=48px, `p-16`=64px. Overriding `--spacing` would risk breaking the *entire* default scale (every other spacing utility derives from it) for zero benefit. Document the mapping instead of touching the base token — a one-line comment in `styles.scss` above the `@theme` block satisfies "documented in a shared config."

### Font loading strategy (undecided in architecture.md — this story makes the call)

`architecture.md` and the UX spec fix the font *choice* (Cormorant Garamond + DM Sans) but never decide *how* to load them, and no font files or CDN links exist anywhere in the repo yet. This story adopts **Google Fonts via `<link>` tags with `preconnect`** for the web (simplest, zero build tooling, acceptable for this foundational story since no real pages exist yet to measure LCP against — NFR1's LCP budget becomes relevant starting Epic 3 and self-hosting can be revisited then if needed) and **the `google_fonts` Flutter package** for mobile (the standard, well-maintained solution — avoids manually sourcing and bundling `.ttf` files). Adding `google_fonts` to `pubspec.yaml` is pre-approved as part of this story's scope, not a new-dependency HALT trigger.

`index.html` additions (exact weights needed per the typography table):
```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@400;500&family=DM+Sans:wght@400;500;600&display=swap" rel="stylesheet">
```

### Full authoritative token reference (source: `_bmad-output/planning-artifacts/ux-design-specification.md`)

**Colors** (lines 278-288) — note there are 9, not 8: epics.md's AC text only names 4 in the "given/when/then" prose, but the full palette table includes an extra hover-accent color not mentioned in the epic summary:

| Token | Hex | Role |
|---|---|---|
| `--color-bg` | `#FFFFFF` | Fond principal |
| `--color-text` | `#111111` | Texte principal |
| `--color-text-secondary` | `#555555` | Texte secondaire |
| `--color-accent` | `#C9A96E` | Accent / CTA |
| `--color-accent-hover` | `#A8864A` | Accent survol (hover state — not in epics.md's summary, but in the authoritative UX spec) |
| `--color-bg-secondary` | `#FAF8F5` | Fond secondaire |
| `--color-border` | `#E5E5E5` | Bordures |
| `--color-success` | `#6B8F71` | Succès |
| `--color-error` | `#C0564A` | Erreur |

**Typography** (lines 294-304): H1/H2 = Cormorant Garamond (400/500 weight respectively); H3, body, labels, buttons, captions = DM Sans (weights 600/400/500/600/400 respectively, buttons get `+0.5px` letter-spacing). This story only needs to load the 2 font families and wire H1/H2 vs. body-level defaults (AC #2) — per-component type-scale application (exact px sizes for H3/labels/captions) happens when those components are built in later epics.

**Spacing** (lines 306-319): base grid 8px; scale 4/8/16/24/32/48/64px; max content width 1280px; page margins 16px mobile / 24-40px desktop. Only the 8px scale is in this story's AC #5 — margins/max-width are for the layout components built in later stories.

**Border-radius** (line 319): `4px` for cards/inputs, `2px` for buttons — matches AC #6 exactly.

**Breakpoints** (lines 646-655): also includes a `2xl: 1536px` tier beyond the 4 named in AC #4 (padding increase only, content stays capped at 1280px) — Tailwind v4's default `2xl` already matches this, so no action needed; it's mentioned here only so you don't think it's missing.

### Flutter file locations — follow architecture.md exactly

`architecture.md`'s proposed Flutter tree (line 543) places the tokens file at `lib/app/theme/design_tokens.dart`, **not** a top-level `lib/design_tokens.dart` — use the nested path so this matches the structure later Epic stories will build on (`lib/app/app.dart`, `lib/app/router.dart` siblings).

### Project Structure Notes

- New Angular files: none beyond edits to `src/styles.scss`, `src/index.html`, and the new `src/app/design-tokens.spec.ts`. No `tailwind.config.ts` file is created (v4 doesn't use one) — if you see references to `tailwind.config.ts` in `architecture.md`'s example tree, that's a v3-era leftover in that doc; follow this story's Dev Notes (v4 CSS-first config), not that file listing.
- New Flutter files: `lib/app/theme/design_tokens.dart`, `lib/app/theme/app_theme.dart`, `test/app/theme/app_theme_test.dart`. `lib/main.dart` gets a one-line theme wiring change only.
- Do not create catalog/cart/checkout/account/auth feature folders in either project — those are Epic 2+/3+ scope per architecture.md's full tree; this story only establishes `app/theme/` (Flutter) and global styles (Angular).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.8: Design System Foundation] — user story + acceptance criteria
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Fondation Visuelle, lines 274-319] — authoritative color/typography/spacing/radius values
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Breakpoints Tailwind, lines 646-655] — authoritative breakpoint table (includes `2xl`)
- [Source: _bmad-output/planning-artifacts/architecture.md line 230] — "Tailwind CSS v4 + Angular CDK... tokens CSS custom properties" decision
- [Source: _bmad-output/planning-artifacts/architecture.md line 543] — Flutter `lib/app/theme/app_theme.dart · design_tokens.dart` proposed structure
- [Source: angular.dev/guide/tailwind, fetched 2026-07 — `ng add tailwindcss` schematic and SCSS `@use` guidance]
- [Source: frontend/mon-ecommerce-web/package.json, src/styles.scss, src/index.html, src/app/app.config.ts — confirmed greenfield state]
- [Source: mobile/mon_ecommerce_mobile/lib/main.dart, pubspec.yaml — confirmed unmodified Flutter template]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `npm install` (frontend, first-time — `node_modules` didn't exist), `npx ng add tailwindcss` (installed `tailwindcss@4.3.3` but schematic itself failed: "Package tailwindcss was found but does not support schematics" — fell back to manual `@tailwindcss/postcss` + `.postcssrc.json` setup per Dev Notes fallback)
- `npx ng build` → succeeded, verified via `grep` that `dist/.../styles.css` contains `color-accent`, `radius-card`, `breakpoint-sm` custom properties
- `CHROME_BIN="...msedge.exe" npx ng test --watch=false --browsers=ChromeHeadless` → 7/7 passed (system has no Chrome install; Edge — Chromium-based — used as the `ChromeHeadless` launcher target)
- Flutter/Dart SDK: confirmed absent from this machine (`flutter`/`dart` not on PATH) — same pre-existing gap already logged in `deferred-work.md` from Story 1.1 ("Android SDK absent"). Flutter files were hand-written and carefully reviewed (API signatures cross-checked against Flutter docs, e.g. `CardThemeData` vs deprecated `CardTheme`) but **`flutter analyze`/`flutter test` could not be run** — needs manual verification once Flutter tooling is available.

### Completion Notes List

- **Deviation from Dev Notes (Angular):** discovered mid-implementation that Tailwind v4's `@theme` block content (even with the `static` keyword) comes out **completely empty** specifically under the `ng test` Karma pipeline, while the identical source works correctly for `ng build` and `ng build --configuration=development`. Root cause not fully identified (likely an `@angular/build:karma`/Tailwind v4 JIT integration quirk on this exact version combo: `tailwindcss@4.3.3` + Angular `19.2.24`). Root-caused via a temporary debug test dumping `document.styleSheets` content, which showed `@layer theme {}` emitted empty. Fixed by adding a plain, explicit `:root { --color-*: ...; }` block in `styles.scss` alongside `@theme` — duplicated values, but decoupled from Tailwind's tree-shaking/JIT reliability entirely, so the tokens are guaranteed available regardless of build pipeline (this is what AC #1 actually requires). Updated `design-tokens.spec.ts` to assert token resolution via inline `style="background-color: var(--color-accent)"` rather than Tailwind utility classes (`bg-accent`), since testing "does Tailwind's JIT correctly generate this utility class under Karma" is a Tailwind-internals concern orthogonal to this story's actual requirement (the custom properties themselves being available).
- Confirmed via `grep` on the `ng build` output that all 9 colors, both font tokens, both radius tokens, and all 4 breakpoints are present in the compiled CSS.
- Flutter side implemented per Dev Notes without deviation, but unverified by tooling (see Debug Log).
- **Code review round (2026-07-06):** 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found 3 patch + 3 defer + 9 dismissed findings, 0 decision-needed. All 3 patches applied: breakpoints added to the `:root` fallback block (previously only in `@theme`), `button`/`input`/`label` added to the explicit DM Sans base-layer rule (previously implicit via inheritance from `body`), and a `GoogleFonts.config.allowRuntimeFetching = false` test guard added. Re-verified Angular: `ng build` + `ng test` both pass (7/7). Deferred: `pubspec.lock` regeneration for `google_fonts` (blocked by the same Flutter-tooling gap), Google Fonts CDN GDPR exposure (routed to Epic 8), and cross-language token-parity automation (routed to post-Story-1.9 CI/CD).

### File List

- `frontend/mon-ecommerce-web/package.json` (added `tailwindcss`, `@tailwindcss/postcss`, `postcss`)
- `frontend/mon-ecommerce-web/.postcssrc.json` (new)
- `frontend/mon-ecommerce-web/src/styles.scss` (Tailwind + design tokens + base layer font rules)
- `frontend/mon-ecommerce-web/src/index.html` (Google Fonts preconnect + stylesheet links)
- `frontend/mon-ecommerce-web/src/app/design-tokens.spec.ts` (new)
- `mobile/mon_ecommerce_mobile/pubspec.yaml` (added `google_fonts` dependency)
- `mobile/mon_ecommerce_mobile/lib/app/theme/design_tokens.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/theme/app_theme.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/main.dart` (wired `AppTheme.lightTheme`)
- `mobile/mon_ecommerce_mobile/test/app/theme/app_theme_test.dart` (new)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (1.8 status)
