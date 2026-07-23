# Story 4.1: Panier Anonyme & Gestion Articles

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to add products to my cart without being logged in and manage quantities,
so that I can prepare my order at my own pace without being forced to register.

## Acceptance Criteria

1. **Given** a visitor adds a product to the cart, **when** `POST /api/v1/cart/items` is called with `productId` and `quantity`, **then** the item is added and the cart is returned with updated totals.
2. **Given** an anonymous cart exists, **when** the visitor logs in, **then** the anonymous cart is merged with their account cart (quantities summed for duplicate items).
3. **Given** `PATCH /api/v1/cart/items/{itemId}` is called with a new quantity of 0, **when** the request is processed, **then** the item is removed from the cart.
4. Anonymous carts are stored with a 24h expiry. **[Adapted — see Dev Notes]**: the AC literally specifies Redis (`cart:{sessionId}`, TTL 24h). The user was explicitly asked and chose: use the existing, already-migrated SQL Server `Cart`/`CartItem` tables (which already have both `UserId` and `SessionId` columns, each indexed — Story 1.3's schema was evidently designed for this) for both anonymous and authenticated carts, with no Redis dependency. 24h expiry is approximated at read/write time (a session-identified cart untouched for 24h is treated as expired and replaced) rather than a native Redis TTL.
5. `DELETE /api/v1/cart/items/{itemId}` removes a specific item.
6. `GET /api/v1/cart` returns the current cart with item details, unit prices, and total in cents.
7. Stock availability is NOT checked at this stage (only at order confirmation).

## Tasks / Subtasks

### Backend — new Cart feature; UI is explicitly out of scope (Story 4.2 builds StickyAddToCart/CartDrawer), matching Story 3.1's own "API first" precedent

- [x] Task 1: Cart identity resolution — anonymous session id transport (AC: #1, #2, #4)
  - [x] **Not cookie-based.** The existing CORS policy (`Program.cs`) uses `AllowAnyOrigin()` in Development, which is incompatible with credentialed (cookie-carrying) cross-origin requests (Angular dev server on :4200, API on :5287) — switching to a stricter, credentials-compatible CORS policy would be a shared-infrastructure change with a blast radius well beyond this story. Instead: the client generates and persists its own anonymous session id (a GUID) and sends it via a plain custom request header, `X-Cart-Session-Id` — not subject to the cookie/credentials CORS restriction (`AllowAnyHeader()` already covers custom headers). Client-side generation/persistence is Story 4.2's job (no Angular/Flutter UI exists yet to own this); the backend accepts and correctly processes the header today, and defensively generates+returns one via a response header when a request arrives with neither an authenticated user nor a session header, so no caller is ever left without an identity to use going forward.
  - [x] `CartOwner` record (`Application/Cart/Models/CartOwner.cs`): `record CartOwner(string? UserId, string? SessionId)` — exactly one must be non-null, enforced by a factory method, not the public constructor directly
- [x] Task 2: `ICartService` / `CartService` (AC: #1, #2, #3, #4, #5, #6, #7)
  - [x] `Application/Common/Interfaces/ICartService.cs`: `GetCartAsync`, `AddItemAsync`, `UpdateItemQuantityAsync` (quantity 0 removes), `RemoveItemAsync`, `MergeAnonymousCartAsync`
  - [x] `Infrastructure/Cart/CartService.cs`: `FindOrCreateCartAsync(CartOwner)` — looks up by `UserId` (authenticated, never expires) or `SessionId` (anonymous); for a `SessionId`-matched cart last modified more than 24h ago, deletes it and creates a fresh one instead of reusing it (the read-time expiry approximation from AC #4's adaptation)
  - [x] Item lookup for update/remove is scoped to the resolved owner's own cart in the SAME query (IDOR prevention — same pattern established since Story 2.4/2.5: never look up a `CartItem` by id alone, then check ownership as a separate step)
  - [x] `MergeAnonymousCartAsync(sessionId, userId)`: finds the anonymous cart by `SessionId` (no-ops cleanly if none exists or it's already expired), finds-or-creates the authenticated cart by `UserId`, sums quantities for matching `ProductId`s and moves over anything new, deletes the anonymous cart row
  - [x] Stock is deliberately never checked here (AC #7) — matches Story 3.1's `InStock` flag being purely informational at the catalogue-browsing layer; stock validation is Epic 4's later order-confirmation story's job
- [x] Task 3: `CartDto`/`CartItemDto` (AC: #1, #6)
  - [x] `Application/Cart/Models/CartItemDto.cs`: `record CartItemDto(Guid Id, Guid ProductId, string ProductName, string? ImageUrl, int UnitPriceInCents, int Quantity, int LineTotalInCents)` — unit price captured at read time from the current `Product.PriceInCents` (no price-snapshotting at add-to-cart time; a cart is a live view of current prices, not a locked-in quote — that's what order confirmation is for)
  - [x] `Application/Cart/Models/CartDto.cs`: `record CartDto(List<CartItemDto> Items, int TotalInCents)`
- [x] Task 4: MediatR commands/queries + validators (AC: #1, #3, #5, #6)
  - [x] `GetCartQuery`, `AddCartItemCommand` (validator: `ProductId` not empty, `Quantity` ≥ 1), `UpdateCartItemCommand` (validator: `Quantity` ≥ 0), `RemoveCartItemCommand` — none `[Authorize]`, same public-query pattern as every catalogue query
- [x] Task 5: `Web/Endpoints/Cart.cs` (AC: #1, #3, #5, #6)
  - [x] `POST /api/v1/cart/items`, `PATCH /api/v1/cart/items/{itemId}`, `DELETE /api/v1/cart/items/{itemId}`, `GET /api/v1/cart` — all `.AllowAnonymous()`
  - [x] Shared `ResolveOwner(IUser, HttpContext)` helper: authenticated → `CartOwner` by `UserId` (ignores any session header present); else reads `X-Cart-Session-Id` request header, or generates+returns one via response header if absent
- [x] Task 6: Merge-on-login (AC: #2)
  - [x] `AuthResponse` gains a `UserId` field (small additive change — `AuthService.IssueTokensAsync` already has the `ApplicationUser` in scope where the response is built)
  - [x] `Web/Endpoints/Auth.cs`'s `Login` endpoint reads `X-Cart-Session-Id` from the request (if present) and, only after a successful login, calls `ICartService.MergeAnonymousCartAsync(sessionId, result.Value.UserId)` — kept at the Web layer (not inside `LoginCommandHandler`) since header/cookie access is an HTTP concern this codebase's MediatR handlers don't otherwise touch
- [x] Task 7: DI registration — `ICartService`/`CartService` registered in `Infrastructure/DependencyInjection.cs`
- [x] Task 8: Backend tests
  - [x] `CartServiceTests.cs`: add creates a cart + item; adding the same product twice increments quantity (not a duplicate row); update to quantity 0 removes the item; update/remove is IDOR-safe (an item belonging to a DIFFERENT owner's cart can't be touched by id alone); an anonymous cart untouched for >24h is treated as expired (replaced, not reused); merge sums quantities for duplicate products and moves over unique ones; merge no-ops cleanly when no anonymous cart exists; `GetCartAsync` computes `UnitPriceInCents`/`LineTotalInCents`/`TotalInCents` correctly from current product prices
  - [x] `AddCartItemCommandValidatorTests.cs`, `UpdateCartItemCommandValidatorTests.cs`

### Verification

- [x] Task 9: Full verification
  - [x] Backend: real .NET 9 SDK via Docker — `dotnet build` + `dotnet test`
  - [x] No Angular/Flutter work this story — deliberately out of scope; Story 4.2 builds the consuming UI (StickyAddToCart/CartDrawer), same "API first" split as Stories 3.1/3.3

## Review Findings

3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor), run in parallel as background agents against the full diff. Findings below are the synthesis after de-duplication; several were independently raised by more than one reviewer, which is noted explicitly.

### Fixed

1. **Duplicate-cart race condition** (raised independently by Blind Hunter and Edge Case Hunter — the most severe finding). `FindOrCreateActiveCartAsync` was a check-then-act with two separate round trips and no transaction: two concurrent requests for a brand-new owner (double-click "add to cart," or two tabs sharing a session id) could each observe "no cart exists" and both insert one, silently splitting the visitor's cart across two rows. Fixed with a DB-level guarantee, not just an application-level check: `CartConfiguration` now has unique, NULL-filtered indexes on `Cart.UserId` and `Cart.SessionId` (migration `AddUniqueCartOwnerIndexes`), plus a `catch (DbUpdateException)` around the create branch's `SaveChangesAsync` that re-fetches and uses the concurrent winner's cart instead of crashing the losing request.
2. **Unpublished product addable to cart** (Blind Hunter / Edge Case Hunter, same underlying gap independently spotted). `AddItemAsync` didn't check `Product.IsPublished`, unlike every other customer-facing catalogue query in this codebase — a caller who knew or guessed a draft product's id could add it to their cart and see its name/price/image via `GET /cart`, leaking unreleased catalogue data through a path not meant to expose it. Fixed: `AddItemAsync` now checks `IsPublished` alongside existence and throws `NotFoundException` for both cases identically (no signal to distinguish "doesn't exist" from "not published yet").
3. **`GetOwnedItemAsync` missing the expiry check** (Acceptance Auditor). `GetCartAsync`/`FindOrCreateActiveCartAsync` both treat an expired anonymous cart as gone, but the item-lookup path used by `PATCH`/`DELETE` didn't — an item id minted before the 24h expiry window could still be mutated after it, inconsistent with what `GET /cart` already showed. Fixed: `GetOwnedItemAsync` now throws `NotFoundException` for an expired cart's item too.
4. **Cart merge failure could crash an otherwise-successful login** (Blind Hunter). `Auth.Login` called `MergeAnonymousCartAsync` after tokens were already issued and persisted; any exception there (transient DB error, etc.) would have turned a successful login into an unhandled 500, even though the user's credentials were already valid and their session already committed. Fixed: the merge call is wrapped in `try/catch`, logging a warning on failure rather than failing the request — the user gets their session either way; a failed merge just means their anonymous cart's items didn't carry over.
5. **Double-remove race surfaced as an unhandled 500** (Edge Case Hunter). Two concurrent `DELETE`s for the same item — a double-click, or a retry racing the original request — hit `DbUpdateConcurrencyException` on the loser, which `ProblemDetailsExceptionHandler` had no case for. Fixed globally (not just for carts): added a `DbUpdateConcurrencyException` → 409 Conflict mapping, reusable by any future feature with the same "modify/delete a row that another request already removed" shape.
6. **Integer overflow in cart totals** (Edge Case Hunter). `LineTotalInCents`/`TotalInCents` were computed as plain `int` arithmetic; `Enumerable.Sum(int)` also uses *checked* arithmetic internally and throws `OverflowException` once accumulated totals exceed `int.MaxValue` — again a case `ProblemDetailsExceptionHandler` had no mapping for. Fixed two ways: `AddCartItemCommandValidator`/`UpdateCartItemCommandValidator` now cap quantity at 1,000 (`MaxQuantity`), and `MapToDto` computes both line and cart totals via `long` arithmetic, clamped to `int.MaxValue`, as defense in depth beyond the validator cap.

### Accepted, documented, not fixed

7. **Lost-update race on quantity** (Blind Hunter). Two concurrent `PATCH` requests updating the same item's quantity (e.g. two open tabs) is a last-write-wins race — no optimistic concurrency token on `CartItem`. Accepted rather than fixed, same reasoning class as Story 3.1's non-atomic cache-version bump and Story 2.3's refresh-token revocation race: a cart quantity is a low-stakes, easily-corrected value (the user can just re-open the cart and see/fix the actual state), and adding a concurrency token here would add real complexity (409-on-conflict handling, client retry logic) for a scenario limited to a single user racing themselves across two tabs — not a cross-user integrity or security concern.
8. **Session-id "griefing" via merge** (Edge Case Hunter). `Auth.Login`'s merge path trusts whatever `X-Cart-Session-Id` the client sends — a caller who somehow knew or guessed a *different* visitor's active anonymous session id could send it at their own login and merge that stranger's cart contents into their own account. Accepted: anonymous session ids are server-generated GUIDs (122 bits of entropy), the same trust model already used for refresh tokens and password-reset tokens elsewhere in this codebase — genuinely learning another visitor's live session id in the first place already implies a worse compromise (e.g. reading their `localStorage` via XSS) than the cart-merge nuisance itself, and the impact if it did happen is limited to item rows moving between carts, not account or payment data.

All 153 backend unit tests (27 of them cart-specific) pass after every fix above, including two new validator boundary tests (`MaxQuantity` and `MaxQuantity + 1`) added for finding #6.

## Dev Notes

### Cart storage: SQL Server for both anonymous and authenticated carts, not Redis

The AC's literal text specifies Redis (`cart:{sessionId}`, TTL 24h) — but the domain schema (`Cart`/`CartItem`, migrated since Story 1.3) already has a `Cart.SessionId` column with its own index (`ix_carts_session_id`), alongside `Cart.UserId` (`ix_carts_user_id`). This is strong evidence the schema was actually designed for SQL-Server-backed anonymous carts, not Redis ones — a real conflict between the AC's literal spec and the already-built, already-migrated schema, structurally the same class of conflict as Story 3.2's PostgreSQL-vs-SQL-Server fork. The user was explicitly asked (not guessed) and chose SQL Server for both cart types: no Redis dependency for a core purchase-funnel feature, fully testable in this environment (Redis isn't configured here — the same standing gap since Story 3.1), and reuses infrastructure that already exists rather than adding a new one. The 24h "TTL" becomes a read-time expiry check (an anonymous cart untouched for 24h is discarded and replaced on next access) rather than a hard Redis-native expiration — an approximation, not identical UX (a genuinely idle anonymous cart lingers in the DB until its next access attempt rather than disappearing exactly at the 24h mark), but faithful to the AC's actual intent (abandoned anonymous carts don't persist forever) without the infrastructure dependency.

### Anonymous identity: a custom header, not a cookie

Cookie-based session identification would need HttpOnly cookies to survive across the Angular (`:4200`) → API (`:5287`) origin boundary during local dev, which requires the browser to treat the request as credentialed — incompatible with the current `AllowAnyOrigin()` CORS policy (`Program.cs`, Development-only). Tightening CORS to a specific-origin + `AllowCredentials()` policy is a reasonable thing to do eventually, but it's a shared, cross-cutting infrastructure change well beyond this one story's scope and risk budget. Using a plain custom header (`X-Cart-Session-Id`) instead sidesteps the whole credentials/CORS question — `AllowAnyHeader()` already permits it — at the cost of the client (Story 4.2) being responsible for generating and persisting the id itself (e.g. `localStorage`) rather than getting it "for free" via an HttpOnly cookie the browser manages automatically. This is an implementation-detail choice made within the user's already-decided "SQL Server for both" resolution, not escalated further.

### A genuine EF Core InMemory provider bug, found and fixed via isolated repro (not a workaround)

`CartService`'s first working draft added new `CartItem`s via the parent's collection navigation (`cart.Items.Add(new CartItem {...})`) — a completely standard, idiomatic EF Core pattern. This threw `DbUpdateConcurrencyException: "Attempted to update or delete an entity that does not exist in the store"` on every `AddItemAsync`/`MergeAnonymousCartAsync` call, but *only* when the parent `Cart` had already been persisted in a *separate*, prior `SaveChangesAsync` call (i.e., exactly the find-or-create-then-mutate shape this service needs). Root-caused via a series of increasingly minimal, isolated repro tests directly against a real `ApplicationDbContext` (no service/mocking involved) rather than assumed or worked around blind:
- Modifying the parent's own scalar property alone (no new child) in a second save: fine.
- Adding a child to a *brand-new, not-yet-saved* parent in the same save as the parent's own creation: fine.
- Adding a child via the parent's navigation collection after the parent was saved in a prior, separate call: **fails**, every time.
- Adding that same child directly to its own DbSet (`_context.CartItems.Add(...)`) instead of via the parent's navigation: **fixed** — confirmed via the identical minimal repro with only that one line changed.

This is a real, narrow blind spot in the EF Core InMemory provider (not present, as far as this session's other stories' experience with `ExecuteUpdateAsync`/negative-`Skip()` suggests, in real SQL Server) — joining this project's growing, explicitly-tracked list of InMemory-provider-specific gaps. `CartService` now adds every new `CartItem` directly to `_context.CartItems`, which is also the more conventional EF Core pattern regardless of this bug, so the fix has no downside even against SQL Server.

### Fixing `dotnet ef migrations add` for this repo (two separate, previously-latent tooling gaps)

Generating the `AddUniqueCartOwnerIndexes` migration (the fix for Finding #1 above) surfaced two pre-existing tooling gaps this repo hadn't hit before, because no prior story had needed `--project src/Infrastructure` to actually work end-to-end:
- `Microsoft.EntityFrameworkCore.Design` (the package providing EF's design-time MSBuild targets) was referenced only by `Web.csproj`, not `Infrastructure.csproj` — the project the documented `--project` flag (CLAUDE.md) actually points at. Added the package reference to `Infrastructure.csproj`.
- This repo's `Directory.Build.props` sets a custom `ArtifactsPath` (the .NET SDK's newer unified-output-layout feature) — *unless* an `EFCoreToolsRunning` MSBuild property is `true`, a guard clearly meant for exactly this situation but never actually exercised until now. EF's design-time build looks for its generated targets file at the *default* `obj/` location; with the custom `ArtifactsPath` active, that file isn't there, and the CLI fails with `MSB4057: The target "GetEFProjectMetadata" does not exist in the project` — a misleading error that doesn't mention `ArtifactsPath` at all. Fixed operationally, not by touching the props file: pass `-e EFCoreToolsRunning=true` to the Docker container (MSBuild picks up matching-named environment variables as property values) before running any `dotnet ef` command.
- Separately, even with both of those fixed, `dotnet ef` still failed — this time by trying to bootstrap the *full* `Web` host (`Program.cs`) to resolve `ApplicationDbContext`, which fails DI validation because unrelated services (`IEmailService`, registered but not configured for this local/no-secrets environment) can't be constructed. Fixed with the standard EF-recommended pattern: added `Infrastructure/Data/ApplicationDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<ApplicationDbContext>`, which builds a minimal `DbContextOptions` directly from `Web/appsettings.json`'s connection string, bypassing the app's DI container entirely at design time.

Together these mean any future story needing a migration from `src/Infrastructure` should run (inside the Docker SDK container): `dotnet tool install --global dotnet-ef --version 9.* && export PATH="$PATH:/root/.dotnet/tools" && dotnet restore MonEcommerce.sln && EFCoreToolsRunning=true dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web` (or pass `-e EFCoreToolsRunning=true` to `docker run` instead of inlining it).

### The backend is fully built and tested; nothing in this story is blocked on Story 4.2

Because the anonymous-session mechanism is a plain header (not something requiring browser-cookie machinery this environment can't exercise), every endpoint in this story — including the header-generation fallback and the login-time merge — is testable today via direct HTTP-shaped unit/service tests with a crafted `X-Cart-Session-Id` value, the same way Story 3.1's endpoints were tested before any consuming UI existed.

## Project Structure Notes

New `Application/Cart/` (Models, Commands, Queries), `Infrastructure/Cart/` (parallel to `Infrastructure/Catalogue/`), `Web/Endpoints/Cart.cs` (parallel to `Products.cs`/`Auth.cs`/`Account.cs`).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.1 acceptance criteria (Epic 4 section); Story 4.2's title confirms UI is a separate, later story
- `backend/MonEcommerce/src/Domain/Entities/Cart.cs`, `CartItem.cs` — the pre-existing, already-migrated schema this story's storage-model decision is based on
- `_bmad-output/implementation-artifacts/3-2-recherche-full-text.md` — precedent for resolving an AC-vs-actual-codebase architecture conflict via `AskUserQuestion`

## Dev Agent Record

### Context Reference

- Architecture conflict (AC's literal Redis spec vs. the already-migrated SQL Server `Cart.SessionId`/`Cart.UserId` schema) surfaced by direct inspection of `Domain/Entities/Cart.cs`/`CartItem.cs`, `CartConfiguration.cs`, and `ApplicationDbContext.cs`; resolved via `AskUserQuestion` — user selected "SQL Server for both cart types." The cookie-vs-header transport decision for anonymous identity was reasoned independently afterward (CORS `AllowAnyOrigin()` incompatibility with credentialed cookies) and treated as an implementation detail within that already-decided scope, not escalated further.

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- EF Core InMemory provider bug (child added via parent navigation after parent already saved in a prior `SaveChangesAsync`) root-caused via a sequence of isolated minimal repro tests against a real `ApplicationDbContext`; fixed by adding new `CartItem`s directly to `_context.CartItems` instead of via `cart.Items.Add(...)` — see Dev Notes.
- `dotnet ef migrations add --project src/Infrastructure` tooling failure (`MSB4057: GetEFProjectMetadata does not exist`) root-caused to this repo's `Directory.Build.props` custom `ArtifactsPath` (only bypassed when `EFCoreToolsRunning=true`); a second, separate failure after that fix (design-time host bootstrap failing DI validation on unrelated services) resolved by adding `ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>` — see Dev Notes.
- Full backend suite: 153/153 passed (`dotnet test MonEcommerce.sln`, real .NET 9 SDK via Docker) after all review fixes; 27/27 Carts-specific tests confirmed separately.

### Completion Notes List

- Implemented cart identity resolution (custom `X-Cart-Session-Id` header, not a cookie — CORS `AllowAnyOrigin()` incompatibility), `CartService` (get/add/update/remove/merge), MediatR commands/queries + validators, `Web/Endpoints/Carts.cs`, merge-on-login in `Auth.cs`.
- Cart storage decision (SQL Server, not Redis) made via `AskUserQuestion`; all other implementation-detail decisions (header vs. cookie, EF InMemory bug fix, migration tooling fixes) made autonomously and documented in Dev Notes.
- 3-layer adversarial review found 6 fixable issues (duplicate-cart race, unpublished-product leak, expired-cart-item mutation, login-merge crash risk, double-remove race, integer overflow) — all fixed and re-verified — and 2 accepted-and-documented low-severity risks (quantity lost-update race, session-id merge griefing). See Review Findings.
- Generated and verified the `AddUniqueCartOwnerIndexes` EF Core migration (unique filtered indexes on `Cart.UserId`/`Cart.SessionId`), resolving two previously-latent migration-tooling gaps in this repo along the way (see Dev Notes) — future stories touching `src/Infrastructure` migrations benefit from this fix too.

### File List

**Domain / Infrastructure (schema & data)**
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/CartConfiguration.cs` (modified — unique filtered indexes)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260723103506_AddUniqueCartOwnerIndexes.cs` + `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` (new/modified)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContextFactory.cs` (new — design-time factory)
- `backend/MonEcommerce/src/Infrastructure/Infrastructure.csproj` (modified — added `Microsoft.EntityFrameworkCore.Design` reference)

**Application layer**
- `backend/MonEcommerce/src/Application/Carts/Models/CartOwner.cs`, `CartDto.cs`, `CartItemDto.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Interfaces/ICartService.cs` (new)
- `backend/MonEcommerce/src/Application/Carts/Queries/GetCartQuery.cs` + Handler (new)
- `backend/MonEcommerce/src/Application/Carts/Commands/AddCartItemCommand.cs` + Handler + Validator (new)
- `backend/MonEcommerce/src/Application/Carts/Commands/UpdateCartItemCommand.cs` + Handler + Validator (new)
- `backend/MonEcommerce/src/Application/Carts/Commands/RemoveCartItemCommand.cs` + Handler (new)
- `backend/MonEcommerce/src/Application/Auth/Models/AuthResponse.cs` (modified — added `UserId`)

**Infrastructure (service)**
- `backend/MonEcommerce/src/Infrastructure/Carts/CartService.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Identity/AuthService.cs` (modified — pass `UserId` into `AuthResponse`)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` (modified — registered `ICartService`)

**Web**
- `backend/MonEcommerce/src/Web/Endpoints/Carts.cs` (new; the `Cart`/`Cart.cs` naming was renamed to `Carts`/`Carts.cs` early on to resolve a CS0118 namespace/type collision with `Domain.Entities.Cart`)
- `backend/MonEcommerce/src/Web/Endpoints/Auth.cs` (modified — merge-on-login, try/catch guard)
- `backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (modified — `DbUpdateConcurrencyException` → 409)

**Tests**
- `backend/MonEcommerce/tests/Application.UnitTests/Carts/Services/CartServiceTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Carts/Commands/AddCartItemCommandValidatorTests.cs`, `UpdateCartItemCommandValidatorTests.cs` (new)
