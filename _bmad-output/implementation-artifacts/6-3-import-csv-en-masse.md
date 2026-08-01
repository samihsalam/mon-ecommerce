# Story 6.3: Import CSV en Masse

Status: done

## Story

As an administrator,
I want to import products in bulk via a CSV file,
so that I can quickly populate the catalogue without entering each product manually.

## Acceptance Criteria

1. Given a valid CSV file with columns: nom, description, prix, catégorie, matière, couleur, stock, when `POST /api/v1/admin/products/import` is called (multipart form), then all valid rows are imported and an import report is returned: `{ created: X, errors: [{ row, reason }] }`.
2. Given a CSV with some invalid rows (missing required field, invalid price), when the import is processed, then valid rows are imported successfully and invalid rows are listed in the error report (import is not rolled back).
3. Given a CSV with 100 products, when the import is processed, then it completes in under 30 seconds.
4. The CSV template is downloadable from the admin UI.
5. Imported products default to "Dépublié" status.
6. Duplicate detection: if a product with the same name exists, it is skipped and listed as a warning.

## Tasks / Subtasks

- [x] Task 1: `Application/Common/Utilities/CsvParser.cs` — a small, dependency-free RFC-4180-ish parser (quoted fields, escaped `""`, comma delimiter). No `CsvHelper`/third-party package added — this codebase has zero CSV dependency today and the format needed is simple enough not to justify one. **Known limitation, documented not fixed**: a field containing a literal newline inside quotes is not supported (line-by-line reading) — acceptable for product name/description/material/color free text, flagged in Dev Notes.
- [x] Task 2: `Application/Catalogue/Models/{ImportProductsResult,ImportRowIssue}.cs` — `ImportProductsResult(int Created, List<ImportRowIssue> Errors, List<ImportRowIssue> Warnings)`, `ImportRowIssue(int Row, string Reason)`. AC #1's literal JSON sample (`{ created, errors: [{ row, reason }] }`) omits a `warnings` field even though AC #6 requires one — extended, not narrowed, same as prior stories' incomplete-AC corrections (e.g. Story 5.4's `EmailDispatchLog`).
- [x] Task 3: `Application/Catalogue/Commands/ImportProductsCsvCommand.cs` + Handler + Validator (AC #1, #2, #3, #5, #6). `[Authorize(Roles = Roles.Administrator)]`. Row numbering starts at 2 (row 1 is the header, matching what an admin sees if they open the CSV in a spreadsheet app). Required columns: `nom`, `description`, `prix`, `catégorie`, `stock`; optional: `matière`, `couleur`. Categories and existing product names are both loaded once up front (not per-row) to avoid N+1 queries against a potentially 100-row file (AC #3's 30s budget). Duplicate detection checks both the database AND names already accepted earlier in the same file (two rows in one CSV with the same name — the second is also a "duplicate," not a second product). Every accepted row is added to the `DbContext` but **all invalid/duplicate rows are simply never added** — a single `SaveChangesAsync()` at the end persists only the valid rows, which already satisfies "import is not rolled back" without needing any explicit transaction/rollback machinery.
- [x] Task 4: `Web/Endpoints/AdminProducts.cs` — `POST {RoutePrefix}/import` (multipart, `IFormFile` → `Stream`, same conversion pattern as Story 6.2's image upload and Story 5.1's return photos) and `GET {RoutePrefix}/import/template` (AC #4 — serves the CSV header row as a downloadable `text/csv` file; the only part of "downloadable from the admin UI" a backend story can implement, no admin frontend exists yet — see Story 6.2's Dev Notes for the established precedent on this gap).
- [x] Task 5: Unit tests — `ImportProductsCsvCommandHandlerTests` (all-valid rows, mixed valid/invalid rows, missing-field row, invalid-price row, unknown-category row, in-file duplicate, existing-DB duplicate, price parsing with both `.` and `,` decimal separators), `CsvParserTests`, and a 100-row timing test for AC #3 (asserts well under 30s, same "generous ceiling, not the literal SLA number" approach as Story 5.4's `EmailDispatchSlaTests`).

## Dev Notes

### `prix` column format: euros with a decimal separator, not raw cents

Every other admin-facing command in this codebase (`CreateProductCommand`/`UpdateProductCommand`, Story 6.1) takes `PriceInCents` as a raw integer, because that's the API's internal contract. A CSV an administrator fills in by hand is a different audience — asking them to type "15000" to mean €150.00 is unnatural and error-prone compared to typing "150.00" or "150,00" (French decimal comma, tolerated by replacing `,` with `.` before parsing). The importer parses `prix` as a `decimal`, requires it `> 0` (same AC #5 rule as 6.1's price validator, just applied to a human-entered value instead of an API integer), and converts to cents via `Math.Round(value * 100)`.

### `catégorie` column: matched by name, not id

An administrator filling a spreadsheet knows category names, not GUIDs. `catégorie` is matched case-insensitively against `Category.Name`; no match is a row-level error ("Catégorie introuvable : {value}"), not a whole-import failure.

### Why one `SaveChangesAsync()` at the end, not per-row

Row-by-row `SaveChangesAsync()` would mean up to 100 round-trips for a 100-row file — directly working against AC #3's 30-second budget for no benefit, since invalid/duplicate rows are filtered out *before* ever being added to the `DbContext`'s change tracker. There is nothing partially-written to roll back; a single bulk save at the end is both faster and already exactly matches AC #2's "not rolled back" requirement.

## Project Structure Notes

New: `Application/Common/Utilities/CsvParser.cs`, `Application/Catalogue/Models/{ImportProductsResult,ImportRowIssue}.cs`, `Application/Catalogue/Commands/ImportProductsCsvCommand.cs` (+ Handler + Validator), unit tests under `tests/Application.UnitTests/Catalogue/Commands/` and `tests/Application.UnitTests/Common/Utilities/`. Modified: `Web/Endpoints/AdminProducts.cs`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 6.3 acceptance criteria (Epic 6 section, line ~1053).
- `_bmad-output/implementation-artifacts/6-1-crud-fiches-produits.md` — `CreateProductCommand`'s validation rules (positive price, required category) this story's per-row validation mirrors.
- `_bmad-output/implementation-artifacts/6-2-gestion-des-images-produit.md` — established precedent for flagging an admin-UI-only AC bullet as out of a backend story's scope, applied again here to AC #4's "downloadable from the admin UI" (the template file itself is served; no UI exists to click a button yet).
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs`'s `CreateReturnRequest` — the established `IFormFile` → Application-layer `Stream` conversion pattern.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- No `CsvHelper`/third-party CSV package existed anywhere in this codebase before this story; writing a small dependency-free parser (Task 1) avoided a `Directory.Packages.props`/csproj/restore round trip for a format simple enough not to need one, and kept the parser fully unit-testable in isolation (`CsvParserTests.cs`).
- Confirmed `Category`/existing-product-name lookups needed to be loaded once up front, not per row — a naive per-row `AnyAsync`/`FirstOrDefaultAsync` against the DbContext would directly threaten AC #3's 30-row-per-second-ish budget (100 rows / 30s) for no reason, since both sets are small and fully cacheable in memory for the duration of one import.

### Completion Notes List

- `POST /api/v1/admin/products/import` (multipart CSV) and `GET /api/v1/admin/products/import/template` (downloadable blank template) both implemented on the existing `AdminProducts` endpoint group, admin-role gated.
- `prix` is parsed as a human-entered decimal (accepts both `150.00` and French `150,00`), not a raw cents integer like the JSON API commands use — documented rationale in Dev Notes. `catégorie` is matched by name, case-insensitively, against existing categories.
- Duplicate detection (AC #6) checks both already-existing database product names AND names already accepted earlier in the same file, so two identically-named rows in one CSV don't both get created.
- Invalid and duplicate rows are simply never added to the `DbContext`'s change tracker, so a single `SaveChangesAsync()` at the end already satisfies AC #2's "not rolled back" requirement without any explicit transaction handling — also the fastest option against AC #3's time budget (one round trip instead of up to 100).
- `ImportProductsResult` extends AC #1's literal `{ created, errors: [{ row, reason }] }` JSON sample with a `warnings` array — required by AC #6 ("listed as a warning") but omitted from the AC's own abbreviated example.
- AC #4 ("downloadable from the admin UI") is only partially implementable by this story — the template *file* is served by a real endpoint; the admin UI to click a download button from doesn't exist anywhere in this codebase yet (same gap flagged in Story 6.2's Dev Notes, applied again here rather than re-litigated).
- Full solution build (`dotnet build MonEcommerce.sln`) and test run (`dotnet test MonEcommerce.sln`) both green: 284/284 Application.UnitTests passing, including 12 new tests (5 `CsvParserTests`, 7 `ImportProductsCsvCommandHandlerTests` covering all-valid, mixed valid/invalid, both duplicate-detection paths, and a 100-row timing assertion for AC #3). `global.json` was temporarily toggled to `rollForward: latestMajor` to build/test on this machine's .NET 10-only SDK, then reverted before commit (verified via `git diff --stat -- global.json` showing no diff). No migration needed — this story adds no new persisted fields.

### File List

**New:**
- `backend/MonEcommerce/src/Application/Common/Utilities/CsvParser.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Models/ImportProductsResult.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Models/ImportRowIssue.cs`
- `backend/MonEcommerce/src/Application/Catalogue/Commands/ImportProductsCsvCommand.cs` (+ Handler, Validator)
- `backend/MonEcommerce/tests/Application.UnitTests/Common/Utilities/CsvParserTests.cs`
- `backend/MonEcommerce/tests/Application.UnitTests/Catalogue/Commands/ImportProductsCsvCommandHandlerTests.cs`

**Modified:**
- `backend/MonEcommerce/src/Web/Endpoints/AdminProducts.cs` (2 new routes: `POST import`, `GET import/template`)
