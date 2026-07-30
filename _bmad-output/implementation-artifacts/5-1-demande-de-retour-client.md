# Story 5.1: Demande de Retour Client

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to initiate a return request for a delivered order,
so that I can exercise my 14-day right of withdrawal without friction.

## Acceptance Criteria

1. **Given** a customer has an order with status "Livrée" (`OrderStatus.Delivered`) within the last 14 days, **when** `POST /api/v1/account/orders/{orderId}/returns` is called with reason and description, **then** a return request is created with status "En attente" and a unique return ID.
2. **Given** the return request is created, **when** `ReturnRequestedEvent` is published, **then** an acknowledgement email is sent to the customer within ≤ 30 seconds. **[Already built]**: `Domain/Events/ReturnRequestedEvent.cs` and `Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` already exist (built ahead of schedule) and already do exactly this — this story only needs to actually publish the event with the right data, do not touch the handler.
3. **Given** an order is older than 14 days or not in "Livrée" status, **when** a return is attempted, **then** a `422 Unprocessable Entity` ProblemDetails response is returned with a clear message.
4. The return form includes: reason (dropdown), description (text), optional photos (Cloudinary upload).
5. The return request is visible in the customer's order detail page.
6. The form is accessible on Angular web and Flutter mobile.

## Tasks / Subtasks

### Backend (AC: #1, #2, #3, #4, #5)

- [x] Task 1: Domain
  - [x] `Domain/Enums/ReturnStatus.cs`: `{ Pending = 1, Approved = 2, Rejected = 3, Refunded = 4 }` — only `Pending` ("En attente") is reachable by this story; `Approved`/`Rejected`/`Refunded` are Story 7.3's (admin processing) and 5.3's (refund) territory, defined now so the enum doesn't need a breaking change later, matching the existing `OrderStatus` convention
  - [x] `Domain/Enums/ReturnReason.cs`: `{ WrongSize = 1, DefectiveProduct = 2, NotAsDescribed = 3, ChangedMind = 4, Other = 5 }` — epics.md's AC only says "reason (dropdown)" without enumerating values; these five are the standard e-commerce return-reason set and match this project's e-commerce domain (clothing/furniture per the catalogue) — a reasonable, unblocking interpretation, not a guess requiring escalation
  - [x] `Domain/Entities/Return.cs` (`BaseAuditableEntity`): `OrderId` (Guid, FK), `Order` (nav), `UserId` (string), `Reason` (`ReturnReason`), `Description` (string), `PhotoUrls` (`List<string>`, empty if none), `Status` (`ReturnStatus`, default `Pending`)
  - [x] `Infrastructure/Data/Configurations/ReturnConfiguration.cs`: FK to `Order` (`OnDelete(DeleteBehavior.Restrict)`, same convention as `Order.ShippingAddress`), `PhotoUrls` stored as a JSON-converted column (`HasConversion` to/from a JSON string — no precedent for a string-list column elsewhere in this codebase, but this is the standard EF Core pattern for a simple string list; do not create a separate child table for this), index on `UserId` and on `OrderId`
  - [x] New migration `AddReturns`
- [x] Task 2: `IApplicationDbContext`/`ApplicationDbContext` — add `DbSet<Return> Returns`
- [x] Task 3: `Application/Returns/Commands/CreateReturnRequestCommand.cs` + Handler + Validator (AC #1, #3)
  - [x] Command: `OrderId`, `Reason` (`ReturnReason`), `Description` (string), `Photos` (`IReadOnlyList<Stream>`? — see Dev Notes on why the file upload itself is handled at the endpoint, not threaded through MediatR as raw `IFormFile`, which the Application layer has no reference to)
  - [x] Validator: `Description` `NotEmpty()`, capped at a reasonable length (e.g. 2000 chars, matching the general "don't accept unbounded text" convention elsewhere in this codebase); `Reason` must be a defined `ReturnReason` enum value
  - [x] Handler: loads the order scoped to the requesting user (`Order.UserId == userId`, same IDOR-prevention pattern as `AccountService.GetOrderDetailAsync`) — `NotFoundException` if missing/not theirs (never distinguish "doesn't exist" from "not yours" — established codebase convention). Checks `order.Status == OrderStatus.Delivered` AND `order.LastModified` is within the last 14 days — **see Dev Notes on why `LastModified` is used as the "delivered on" date, a documented interim approximation, not `Created`**. Either check failing throws a new `ReturnWindowExpiredException` (or reuse `ConflictException` — see Dev Notes) mapped to 422 (AC #3's exact status code — note this is 422, matching `ValidationException`'s existing mapping, NOT `ConflictException`'s 409; add a dedicated exception + handler case, don't misuse an existing 409-mapped one just because the message shape is similar)
  - [x] On success: creates the `Return` (uploading any provided photo streams via `IFileStorageService.UploadAsync` first, collecting the returned `Url`s into `PhotoUrls`), publishes `ReturnRequestedEvent(returnId, orderId, customerEmail, reason.ToString())` — needs the customer's email, same `IIdentityService.GetEmailAsync` added in Story 4.6, reused here
- [x] Task 4: `Application/Common/Exceptions/ReturnWindowExpiredException.cs` + `Web/Infrastructure/ProblemDetailsExceptionHandler.cs` mapping → 422 (AC #3)
- [x] Task 5: `Web/Endpoints/Account.cs` — add `POST orders/{orderId:guid}/returns`, `[FromForm]` binding (`IFormFile[]? photos`, `reason`, `description` as form fields — this is a multipart/form-data endpoint, the first one in this codebase; needs `.DisableAntiforgery()` per ASP.NET Core's Minimal API file-upload convention since this project has no antiforgery middleware configured but Minimal APIs require the call explicitly for form-bound endpoints) — converts each `IFormFile` to a `Stream` before dispatching `CreateReturnRequestCommand` (keeps `Stream`, not `IFormFile`, in the Application-layer command — `IFormFile` is an ASP.NET Core/Web-layer type)
- [x] Task 6: `Application/Account/Models/OrderDetailDto.cs` + `AccountService.GetOrderDetailAsync`/`BuildOrderDetailDto` — add a `ReturnSummaryDto? Return` field (null if none requested) so the order detail page can show the return's status (AC #5); `ReturnSummaryDto(Guid Id, string Status, string Reason, DateTimeOffset Created)`
- [x] Task 7: Backend tests
  - [x] `CreateReturnRequestCommandHandlerTests`: creates a return for a `Delivered` order within 14 days; throws the 422 exception for a non-`Delivered` order; throws it for a `Delivered` order older than 14 days (by `LastModified`); throws `NotFoundException` for another user's order (IDOR guard); uploads photos via a mocked `IFileStorageService` and stores the returned URLs; publishes `ReturnRequestedEvent` with the right fields
  - [x] `CreateReturnRequestCommandValidatorTests`: empty/too-long description fails; unknown reason fails
  - [x] `AccountServiceTests`/`AccountServiceOrdersTests` addition: `GetOrderDetailAsync` includes the return summary once one exists

### Frontend — Angular (AC: #4, #5, #6)

- [x] Task 8: `features/account/orders.store.ts` — extend `OrderDetail` with an optional `return` field; add `requestReturn(orderId, reason, description, photos: File[])` using `FormData` (multipart), same store/HTTP-call-per-method convention as every other store in this codebase
- [x] Task 9: New `features/account/pages/return-request/return-request.component.ts` (parallel to `order-detail`) — reason `<select>` (5 values, French labels), description `<textarea>`, optional multi-file photo input, submits via `OrdersStore.requestReturn`; on success navigates back to `/compte/commandes/{orderId}`; on the 422 (window-expired/wrong-status) response, shows the backend's own message inline rather than a generic error (matches this codebase's established "surface the real validation message" convention, e.g. `RegisterCommandValidator`'s duplicate-email 422 already does this on the register page)
- [x] Task 10: `order-detail.component.html` — show a "Demander un retour" button only when eligible (`order.status === 'Livrée'` and within the 14-day window, computed client-side from `order.date` **as an approximation of the same `LastModified`-based check the backend enforces authoritatively** — the backend is still the source of truth for AC #3, this is just to avoid showing a button that will always 422); once a return exists, show its status instead of the button (AC #5)
- [x] Task 11: New route `compte/commandes/:orderId/retour`, `authGuard`-protected
- [x] Task 12: Frontend tests (component + store)

### Mobile — Flutter (AC: #4, #5, #6)

- [x] Task 13: `features/account/providers/orders_provider.dart` (or wherever `ordersProvider`/`OrderDetail` model lives) — mirror the same `return` field + `requestReturn` method, using `dio`'s `FormData`/`MultipartFile` for the photo upload (matching this project's established `dio`-based `api_client.dart` pattern)
- [x] Task 14: New `features/account/screens/return_request_screen.dart` — same fields/flow as the Angular page; **apply the `WidgetsBinding.instance.addPostFrameCallback` pattern for any provider modification called from `initState`** (see the just-fixed bug class in `catalogue_screen.dart`/`order_detail_screen.dart`/`product_detail_screen.dart` — do not reintroduce it in this new screen)
- [x] Task 15: `order_detail_screen.dart` — same eligibility-gated button / return-status display as the Angular page
- [x] Task 16: Add the route in the app's router config

### Verification

- [x] Task 17: Full verification
  - [x] Backend: `dotnet build` + `dotnet test` green
  - [x] Frontend: `npm run build` (production SSR) + `npm test` green
  - [x] Mobile: this codebase's Flutter app can now actually be run (Flutter SDK was just installed this session) — run `flutter analyze` at minimum; running the new screen live is a bonus, not a hard gate, given the empty local database

## Dev Notes

### Why `Order.LastModified` stands in for "date delivered" — a documented interim gap, not a full fix

`Order` has no dedicated `DeliveredAt` timestamp, and no command in this codebase ever transitions `Order.Status` away from `OrderStatus.Pending` today — `UpdateOrderStatus`/`AddTrackingNumber` (Epic 7, Story 7.2) don't exist yet. This means AC #1's precondition ("an order with status Livrée") is, right now, only reachable via seeded/test data, not through any real app workflow — the same class of "not reachable in production yet, but this story's own logic now depends on it being meaningful" situation Story 4.3 hit with address ordering. Rather than block on Story 7.2 (which would delay this entire story for a dependency that isn't this story's job to build), `Order.LastModified` (already bumped on any entity change, inherited from `BaseAuditableEntity`) is used as the best available proxy for "when this order's status last changed" — since nothing else updates an `Order` post-creation today, it's currently equivalent to a `DeliveredAt` in practice. **Flag for Story 7.2**: once a real status-transition command exists, it should either set a dedicated `DeliveredAt` field (preferred, precise) or this story's 14-day check should be revisited — noting this now so it isn't silently forgotten.

### Why a new exception instead of reusing `ConflictException`

AC #3 explicitly requires `422 Unprocessable Entity`, not `409 Conflict` — `ConflictException` is already wired to 409 in `ProblemDetailsExceptionHandler` and reused for genuinely conflict-shaped cases (e.g. Story 4.5's "cart already empty"). A return-window/status rejection is a validation-shaped rejection (the request is well-formed but the resource doesn't qualify), matching `ValidationException`'s existing 422 mapping in spirit — but `ValidationException` is FluentValidation-specific (carries structured per-field errors) and doesn't fit a single free-text business message. A small, dedicated `ReturnWindowExpiredException` mapped to 422 is the correct, minimal addition — not overengineering, since AC #3's exact status code is non-negotiable and no existing exception type both means the right thing semantically and already maps to 422.

### Why the endpoint is multipart/form-data, not JSON

AC #4's "optional photos (Cloudinary upload)" is the first real file-upload requirement anywhere in this codebase — `IFileStorageService`/`CloudinaryFileStorageService` were built in an earlier story but have had zero consumers until now. A JSON body can't carry binary file data directly (base64-encoding photos into JSON is a legitimate alternative some APIs use, but ASP.NET Core Minimal APIs' native `IFormFile`/`[FromForm]` binding is the standard, size-limit-aware, streaming-capable approach — reinventing a base64 upload path here would be the wheel-reinvention this process explicitly warns against).

### Established conventions this story must follow

- IDOR guard: every order/return lookup scoped by `UserId` in the query itself, never a separate check after an unscoped load (Story 2.5, 4.6's precedent)
- `ReturnRequestedEvent`/`ReturnRequestedEmailHandler`: already built, reuse directly, do not duplicate
- `IIdentityService.GetEmailAsync`: already built (Story 4.6), reuse for resolving the customer's email
- Angular: Reactive Forms + the established inline-error pattern (`register.component.html`); Flutter: `WidgetsBinding.instance.addPostFrameCallback` for any provider modification triggered from a lifecycle method (see this session's bug fixes)

## Project Structure Notes

New `Domain/Entities/Return.cs`, `Domain/Enums/ReturnStatus.cs`/`ReturnReason.cs`, `Application/Returns/Commands/`, `Application/Common/Exceptions/ReturnWindowExpiredException.cs`, `Infrastructure/Data/Configurations/ReturnConfiguration.cs`. Frontend: `features/account/pages/return-request/`. Mobile: `features/account/screens/return_request_screen.dart`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 5.1 acceptance criteria (Epic 5 section, line ~893)
- `backend/MonEcommerce/src/Domain/Events/ReturnRequestedEvent.cs`, `Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` — already built, reused as-is
- `backend/MonEcommerce/src/Application/Common/Interfaces/IFileStorageService.cs`, `Infrastructure/ExternalServices/CloudinaryFileStorageService.cs` — existing Cloudinary integration, first real consumer
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` — `GetOrderDetailAsync`/`BuildOrderDetailDto`, extended for the return summary
- `_bmad-output/implementation-artifacts/4-6-confirmation-commande-et-anti-overselling.md` — `IIdentityService.GetEmailAsync`, IDOR-guard convention, IsAuthenticated user-scoping pattern this story reuses

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 warnings, 0 errors. `dotnet test MonEcommerce.sln` — 197/197 passed (`Application.UnitTests`; `Domain.UnitTests`/`Infrastructure.IntegrationTests` report "no tests" — pre-existing, unrelated to this story). New migration `AddReturns` generated cleanly.
- Frontend: `npx ng test --watch=false --browsers=ChromeHeadless` — 146/146 passed. `npm run build` (production SSR) — green, `return-request-component` chunk present, 13 static routes prerendered.
- Mobile: `flutter pub get` (added `image_picker`) and `flutter analyze` — 0 errors (2 pre-existing info/warning items in unrelated files, not touched by this story). Live-running the new screen was scoped as a bonus, not a hard gate (empty local database) — not exercised this pass, `flutter analyze` was the verification gate.
- Fixed one Dart parser ambiguity found while writing `requestReturn`: `cond ? x as String? : null` — a nullable-type cast (`as String?`) immediately before a ternary's `:` is genuinely ambiguous to the Dart parser (`error - Expected an identifier`). Fixed by parenthesizing the cast: `cond ? (x as String?) : null`.
- Test-writing bug (mine, not product code) caught by the first `dotnet test` run: one handler test seeded an order but forgot `SaveChangesAsync` before invoking the handler, so the in-memory provider's query correctly found nothing yet — fixed by adding the missing save.

### Completion Notes List

- Backend: new `Return` entity/`ReturnStatus`/`ReturnReason` enums, `CreateReturnRequestCommand`/Handler/Validator, and the first file-upload endpoint in this codebase (`POST /api/v1/account/orders/{orderId}/returns`, multipart/form-data, `IFormFile` converted to the Application layer's own `ReturnPhotoUpload` to keep ASP.NET Core types out of Application). Reused `ReturnRequestedEvent`/`ReturnRequestedEmailHandler` (already built ahead of schedule) and `IIdentityService.GetEmailAsync` (Story 4.6) as-is.
- New `ReturnWindowExpiredException` → 422, distinct from the existing `ConflictException` → 409, since AC #3 requires exactly 422 and no existing exception both means the right thing and maps to the right status code.
- Documented, not silently invented: `Order.LastModified` stands in for a "delivered on" timestamp that doesn't exist yet in the schema — no command in this codebase transitions `Order.Status` away from `Pending` today, so this AC's precondition is only reachable via seeded data until Story 7.2 exists. Flagged explicitly in Dev Notes for that future story rather than left implicit.
- `AccountService.GetOrderDetailAsync`/`GetOrderByPaymentIntentAsync` both now surface a `ReturnSummaryDto?` via a shared `BuildOrderDetailDtoAsync` (made async to query the return) — AC #5.
- Frontend (Angular) and mobile (Flutter) both implement the same reason-dropdown/description/optional-photos flow against the new endpoint, each following its own codebase's established store/provider conventions. The Flutter screen applies the `WidgetsBinding.instance.addPostFrameCallback` pattern from this session's earlier bug-fix commit — no new instance of that Riverpod crash introduced.
- Added `image_picker` as this Flutter project's first file-picker dependency (photo upload, AC #4).

### File List

**Backend**
- `backend/MonEcommerce/src/Domain/Enums/ReturnStatus.cs`, `ReturnReason.cs` (new)
- `backend/MonEcommerce/src/Domain/Entities/Return.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/ReturnConfiguration.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260730111749_AddReturns.cs` + `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` (new/modified)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IApplicationDbContext.cs` (modified — `DbSet<Return>`)
- `backend/MonEcommerce/src/Infrastructure/Data/ApplicationDbContext.cs` (modified)
- `backend/MonEcommerce/src/Application/Returns/Models/CreateReturnRequestResponse.cs`, `ReturnPhotoUpload.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/Commands/CreateReturnRequestCommand.cs`, `CreateReturnRequestCommandHandler.cs`, `CreateReturnRequestCommandValidator.cs` (new)
- `backend/MonEcommerce/src/Application/Common/Exceptions/ReturnWindowExpiredException.cs` (new)
- `backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (modified — 422 mapping)
- `backend/MonEcommerce/src/Web/Endpoints/Account.cs` (modified — `POST orders/{orderId}/returns`)
- `backend/MonEcommerce/src/Application/Account/Models/OrderDetailDto.cs` (modified), `ReturnSummaryDto.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs` (modified — `BuildOrderDetailDtoAsync`, return-label mapping)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/Commands/CreateReturnRequestCommandHandlerTests.cs`, `CreateReturnRequestCommandValidatorTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceOrdersTests.cs` (modified)

**Frontend**
- `frontend/mon-ecommerce-web/src/app/features/account/orders.store.ts`, `orders.store.spec.ts` (modified — `requestReturn`, `ReturnSummary`)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/order-detail/order-detail.component.ts`, `.html`, `.spec.ts` (modified — eligibility-gated button / return status)
- `frontend/mon-ecommerce-web/src/app/features/account/pages/return-request/return-request.component.ts`, `.html`, `.scss`, `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — `compte/commandes/:orderId/retour`)

**Mobile**
- `mobile/mon_ecommerce_mobile/pubspec.yaml`, `pubspec.lock` (modified — `image_picker`)
- `mobile/mon_ecommerce_mobile/lib/features/account/providers/orders_provider.dart` (modified — `ReturnReason`, `ReturnSummary`, `requestReturn`)
- `mobile/mon_ecommerce_mobile/lib/features/account/screens/order_detail_screen.dart` (modified — eligibility-gated button / return status)
- `mobile/mon_ecommerce_mobile/lib/features/account/screens/return_request_screen.dart` (new)
- `mobile/mon_ecommerce_mobile/lib/app/router.dart` (modified — `compte/commandes/:orderId/retour`)
