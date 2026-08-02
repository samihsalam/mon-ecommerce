# Story 8.1: Pages Légales

Status: done

## Story

As a visitor,
I want to access the CGV, privacy policy, and returns policy pages,
so that I can understand my rights and the platform's terms before purchasing.

## Acceptance Criteria

1. Given a visitor navigates to `/cgv`, `/confidentialite`, or `/retours`, when the page loads, then the full legal content is displayed without requiring login.
2. Given any page on the platform, when the footer is rendered, then links to all three legal pages are visible and accessible.
3. All legal pages are server-side rendered (Angular SSR) and indexable by search engines.
4. Pages use DM Sans typography and the Élégance Naturelle palette.
5. Legal content is reviewed and approved before the platform launches publicly. **Not satisfiable by this story** — see Dev Notes.
6. Pages include `<title>` and `<meta description>` for SEO.

## Tasks / Subtasks

- [x] Task 1: `SeoService` gains a generic `setStaticPageSeo(title, description)` method (AC #6) — the existing `setProductSeo` is product-specific (Open Graph, JSON-LD, price); a static legal page only ever needs `<title>`/`<meta description>`, the same minimal subset every one of the three pages in this story needs identically.
- [x] Task 2: `core/components/footer/footer.component.{ts,html,scss,spec.ts}` (AC #2) — links to `/cgv`, `/confidentialite`, `/retours`, added to `app.component.html` alongside the existing header so it renders on every route.
- [x] Task 3: Three new standalone page components under `features/legal/pages/{cgv,privacy-policy,returns-policy}/` (AC #1, #3, #4, #6), routed at `/cgv`, `/confidentialite`, `/retours` in `app.routes.ts` (lazy `loadComponent`, no `authGuard` — public pages, AC #1's "without requiring login"). No data fetching, no `OnInit` — purely static content, so SSR "just works" the same way every other route component already does (nothing async to await before first paint). Content styled with the existing `font-heading`/`font-body`/`--color-*` design tokens (AC #4) — no new tokens introduced.
- [x] Task 4: Unit tests — `footer.component.spec.ts` (renders all three legal links) and one spec per legal page (SEO title/description set, content renders, no login required to construct — i.e. no `authGuard`/auth dependency at all).

## Dev Notes

### AC #5 cannot be satisfied by this or any other engineering story

"Legal content is reviewed and approved before the platform launches publicly" describes a *business/legal* process (external counsel or a compliance owner signing off), not a software capability — there's no code that could make this AC "true." The CGV/privacy-policy/returns-policy text this story ships is realistic, generically-appropriate placeholder copy for a French e-commerce site (matching this codebase's own established business rules — e.g., the returns policy explicitly states the 14-day return window `CreateReturnRequestCommandHandler`'s `ReturnWindow` constant already enforces server-side, so the two don't contradict each other), but it is emphatically **not** reviewed or approved legal copy and must not be mistaken for it before any real launch. Flagged here in the strongest terms this codebase's Dev Notes convention allows, rather than silently checked off.

### No admin content-management capability

The AC doesn't ask for legal content to be editable via an admin UI (unlike, say, Epic 6's product catalogue) — these three pages are static Angular content, the same as how `HomeComponent`'s placeholder copy works today. If legal text ever needs to change without a code deploy, that would be new, unrequested scope for a future story, not something quietly built in here.

## Project Structure Notes

New: `core/components/footer/footer.component.{ts,html,scss,spec.ts}`, `features/legal/pages/{cgv,privacy-policy,returns-policy}/*.component.{ts,html,scss,spec.ts}`. Modified: `app.routes.ts`, `app.component.ts`/`app.component.html`, `core/services/seo.service.ts`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 8.1 acceptance criteria (Epic 8 section, line ~1267) — the first story in Epic 8.
- `frontend/mon-ecommerce-web/src/app/core/services/seo.service.ts` — the existing `setProductSeo` pattern this story's `setStaticPageSeo` extends.
- `backend/MonEcommerce/src/Application/Returns/Commands/CreateReturnRequestCommandHandler.cs` — the 14-day `ReturnWindow` the returns-policy page's copy stays consistent with.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Verified `ng build`'s prerendering discovered all three new static routes automatically (`Prerendered 16 static routes`, confirmed via `dist/mon-ecommerce-web/browser/{cgv,confidentialite,retours}/index.html` existing with the correct `<title>`/`<meta description>`/content baked into the static HTML) — stronger than plain SSR for AC #3's "indexable by search engines" (a crawler gets real static HTML, no render step needed at all), and required no extra configuration since these are parameterless, data-free routes exactly like every other prerenderable route already in this app.
- Full Angular unit test suite run (`ng test --watch=false --browsers=ChromeHeadless`): 154/154 passing, including the 8 new/changed specs (footer, 3 legal pages × 2 tests each, `SeoService.setStaticPageSeo`).

### Completion Notes List

- `/cgv`, `/confidentialite`, `/retours` all added as public (no `authGuard`) lazy-loaded routes (AC #1), each a static-content standalone component with no data fetching.
- New `FooterComponent` rendered once in `app.component.html` (alongside the existing header), linking to all three legal pages on every route (AC #2).
- `SeoService.setStaticPageSeo(title, description)` added as a minimal counterpart to the existing product-specific `setProductSeo` — each legal page calls it with its own title/description (AC #6).
- Pages use only the existing `font-heading`/`font-body`/`--color-*` Tailwind design tokens already established for the rest of the app (AC #4) — no new tokens introduced.
- AC #5 ("legal content is reviewed and approved before the platform launches publicly") is explicitly flagged as not satisfiable by this or any engineering story — the shipped CGV/privacy-policy/returns-policy text is realistic placeholder copy (internally consistent with the backend's own 14-day return window), not reviewed/approved legal text, and must not be mistaken for it before any real launch.
- `ng build` succeeds cleanly; `ng test` 154/154 passing.

### File List

**New:**
- `frontend/mon-ecommerce-web/src/app/core/components/footer/footer.component.{ts,html,scss,spec.ts}`
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/cgv/cgv.component.{ts,html,spec.ts}`
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/privacy-policy/privacy-policy.component.{ts,html,spec.ts}`
- `frontend/mon-ecommerce-web/src/app/features/legal/pages/returns-policy/returns-policy.component.{ts,html,spec.ts}`

**Modified:**
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (3 new routes)
- `frontend/mon-ecommerce-web/src/app/app.component.ts` / `app.component.html` (`<app-footer />`)
- `frontend/mon-ecommerce-web/src/app/core/services/seo.service.ts` (+ `.spec.ts`) (`setStaticPageSeo`)
