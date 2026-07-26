# Story 4.5: Checkout Étape 3 — Paiement Stripe

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to pay by credit card via Stripe securely,
so that my order is paid without my card data ever reaching the server.

## Acceptance Criteria

1. **Given** the customer reaches checkout step 3, **when** `POST /api/v1/payments/create-intent` is called with the cart total, **then** a `clientSecret` is returned and no card data is stored server-side.
2. **Given** the customer enters card details in the Stripe.js form, **when** payment is submitted, **then** Stripe processes the payment client-side using the `clientSecret`.
3. **Given** the payment is declined, **when** Stripe returns an error, **then** an inline error message is shown below the payment form ("Paiement refusé. Vérifiez vos informations.") without losing the cart.
4. The `OrderStepIndicator` shows "Étape 3/4 — Paiement" as active.
5. The Stripe payment form is embedded (Stripe Elements), never a custom card input.
6. HTTPS is enforced on the payment page. **[Already satisfied — see Dev Notes]**: existing infrastructure (`Program.cs`'s `app.UseHsts()` outside Development), no new work needed.
7. The order summary (items, shipping, total) is visible alongside the payment form.
8. Flutter mobile: **[Scoped — see Story 4.3's Dev Notes]** out of scope, same standing decision as Stories 4.3/4.4.

## Tasks / Subtasks

### Backend — reuse existing Stripe plumbing (AC: #1, #2)

- [x] Task 1: Extract a shared shipping-options catalog (refactor, no behavior change)
  - [x] New `Application/Shipping/ShippingOptionsCatalog.cs`: a static class holding the exact `IReadOnlyList<ShippingOptionDto>` currently inlined in `GetShippingOptionsQueryHandler` (Story 4.4), plus `TryGetById(string id, out ShippingOptionDto? option)`
  - [x] `GetShippingOptionsQueryHandler` now reads from the catalog instead of its own private field — this story's payment-intent handler needs the exact same list as its one source of truth for prices (never trust a client-sent shipping cost — see Task 2), so a single shared catalog prevents the two from ever drifting out of sync
- [x] Task 2: `CreatePaymentIntentCommand` (AC: #1)
  - [x] New `Application/Payments/Models/CreatePaymentIntentResponse.cs`: `record CreatePaymentIntentResponse(string ClientSecret);` — deliberately minimal, exactly what AC #1 asks for; the order summary (AC #7) is assembled client-side from data the frontend already has (`CartStore`/`CheckoutStore`), not duplicated here
  - [x] New `Application/Payments/Commands/CreatePaymentIntentCommand.cs`: `record CreatePaymentIntentCommand(string UserId, string ShippingOptionId) : IRequest<CreatePaymentIntentResponse>;` — `UserId` not a full `CartOwner`: payment is only reachable by an authenticated customer (checkout already requires login since Story 4.3), so there's no anonymous-cart case to support here, unlike `Carts.cs`
  - [x] New `Application/Payments/Commands/CreatePaymentIntentCommandValidator.cs`: `ShippingOptionId` must be non-empty AND must resolve via `ShippingOptionsCatalog.TryGetById` — an unknown id is a 422, not silently ignored
  - [x] New `Application/Payments/Commands/CreatePaymentIntentCommandHandler.cs`: loads the cart via `ICartService.GetCartAsync(CartOwner.ForUser(request.UserId))`; throws `ConflictException` ("Le panier est vide.") if `cart.Items.Count == 0` — a payment intent for an empty cart is nonsensical and shouldn't silently create a €0 charge attempt; resolves the shipping price via `ShippingOptionsCatalog.TryGetById` (never trusts a client-sent price — same "server is the source of truth for money" principle already established for `Product.PriceInCents` in `CartService.AddItemAsync`); calls `IPaymentService.CreatePaymentIntentAsync(cart.TotalInCents + shippingOption.PriceInCents)`; returns `CreatePaymentIntentResponse(result.ClientSecret)`
- [x] Task 3: `Web/Endpoints/Payments.cs` (AC: #1)
  - [x] `RoutePrefix => "/api/v1/payments"`, `MapPost(CreateIntent, "/create-intent").RequireAuthorization()` — payment creation is inherently tied to an authenticated identity, same reasoning as the checkout `authGuard` on the frontend
  - [x] Request body: `record CreatePaymentIntentRequest(string ShippingOptionId);` (defined in the endpoint file, same convention as `Carts.cs`'s `AddCartItemRequest`)
- [x] Task 4: Backend tests
  - [x] `CreatePaymentIntentCommandHandlerTests.cs`: creates a payment intent with `amount = cart total + shipping price` (assert the exact amount passed to a faked `IPaymentService`); throws `ConflictException` for an empty cart; the response's `ClientSecret` matches what `IPaymentService` returned
  - [x] `CreatePaymentIntentCommandValidatorTests.cs`: fails for an empty/unknown `ShippingOptionId`; passes for `"standard"`/`"express"`
  - [x] `ShippingOptionsCatalog` refactor: re-run `GetShippingOptionsQueryHandlerTests.cs` unchanged — confirms the extraction didn't change Story 4.4's already-shipped behavior

### Frontend — Angular only (AC: #8)

- [x] Task 5: Stripe.js dependency + config
  - [x] `npm install @stripe/stripe-js` — the official Stripe JS SDK loader; nothing else needed (no `@stripe/stripe-angular` wrapper — this codebase has no precedent for third-party Angular integration wrappers, and Stripe's own vanilla JS API is small enough not to need one)
  - [x] Add `stripePublishableKey: ''` to `environment.ts`/`environment.production.ts`, matching the exact placeholder-with-comment pattern already used for `sentryDsn` — **this story cannot be end-to-end verified against live Stripe in this dev environment**: `Stripe:SecretKey` is empty in `appsettings.json` (so `IPaymentService` isn't even registered in DI — see `DependencyInjection.cs`'s conditional registration), and there is no publishable key either. Same class of environment gap as Redis (Story 4.1) and Flutter's missing SDK — documented, not blocking, verified via mocks/unit tests instead of a live call
- [x] Task 6: Extend `CheckoutStore` (AC: #1)
  - [x] Add `createPaymentIntent(shippingOptionId: string): Promise<string | null>` — `POST /api/v1/payments/create-intent`, returns the `clientSecret` on success or `null` on failure (setting a `paymentError` state field, same error-surfacing lesson already applied twice this epic — Stories 4.3 and 4.4's reviews both caught a silently-swallowed HTTP failure the first time; this store gets it right from the start)
- [x] Task 7: Checkout payment page (AC: #2, #3, #4, #5, #7)
  - [x] New `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-payment/checkout-payment.component.ts` (parallel naming to `checkout-address`/`checkout-shipping`)
  - [x] On init: guard on `CheckoutStore.address()` AND `CheckoutStore.shippingOption()` both being set (same step-order-guard pattern added to `checkout-shipping` in Story 4.4's review — redirect to whichever earlier step is missing); call `CheckoutStore.createPaymentIntent(shippingOption.id)`; on success, load Stripe.js (`loadStripe(environment.stripePublishableKey)`), create an `Elements` instance with the returned `clientSecret`, and mount a `PaymentElement` (not a custom card input — AC #5) into a template `div` via `AfterViewInit` (Stripe Elements mounts to a raw DOM node, not a normal Angular binding)
  - [x] Order summary (AC #7): cart items from `CartStore.items()`, the chosen shipping option's name/price from `CheckoutStore.shippingOption()`, and the grand total (`CartStore.totalInCents() + shippingOption.priceInCents`) — all already-available client state, no new backend call
  - [x] On form submit: `stripe.confirmPayment({ elements, redirect: 'if_required' })` — `redirect: 'if_required'` avoids a full-page redirect for card payments that don't need 3DS/off-site auth, keeping the customer on this page for the common case (still correctly falls back to a redirect for payment methods that need one)
  - [x] Decline handling (AC #3): `confirmPayment`'s returned `error` → show "Paiement refusé. Vérifiez vos informations." in a `role="alert"` paragraph below the form, exactly as specified; do NOT clear `CartStore`/`CheckoutStore` on this path — a decline must leave the customer able to retry immediately, matching the AC's explicit "without losing the cart"
  - [x] Mount `<app-order-step-indicator [currentStep]="3" />` at the top of the page
  - [x] Clean up the mounted Stripe `Elements` instance in `ngOnDestroy` (Stripe Elements doesn't auto-unmount when its host component is destroyed by Angular's router)
- [x] Task 8: Routing (AC: #1)
  - [x] New route `checkout/paiement` in `app.routes.ts`, `canActivate: [authGuard]` — same reasoning as Stories 4.3/4.4; this is also the exact route Story 4.4's shipping page already navigates to, so step 2 → step 3 becomes a real transition for the first time
- [x] Task 9: Frontend tests
  - [x] `checkout.store.spec.ts` additions: `createPaymentIntent` returns the `clientSecret` on success; sets `paymentError` and returns `null` on failure
  - [x] `checkout-payment.component.spec.ts`: redirects to `/checkout/adresse` or `/checkout/livraison` when either is missing; calls `createPaymentIntent` with the chosen shipping option's id; renders the order summary (items, shipping, total) from `CartStore`/`CheckoutStore`; shows the exact decline message on a failed `confirmPayment` without clearing cart/checkout state — Stripe.js itself (`loadStripe`, `Elements`, `PaymentElement`) must be mocked/stubbed for these tests, since it's a third-party script loaded at runtime, not something Karma/Jasmine can exercise for real (no live Stripe account exists in this environment anyway — see Task 5)

### Verification

- [x] Task 10: Full verification
  - [x] Backend: `dotnet build` + `dotnet test` (Docker, .NET 9 SDK) green
  - [x] Frontend: `npm run build` (production SSR) + `npm test` (Karma/Jasmine) green
  - [x] Explicitly NOT verified: an actual live payment against Stripe's sandbox (no test-mode keys configured in this environment — see Task 5's Dev Notes)

## Dev Notes

### Stripe backend plumbing already exists — this story wires it up, doesn't build it from scratch

`IPaymentService`/`StripePaymentService`/`PaymentIntentResult` (`Application/Common/Interfaces/IPaymentService.cs`, `Infrastructure/ExternalServices/StripePaymentService.cs`) were already built in Stories 1.5/1.6, including `AutomaticPaymentMethods.Enabled = true` on the created `PaymentIntentCreateOptions` — which is exactly what pairs with Stripe's modern `PaymentElement` (not the older `CardElement`), confirming the AC's "Stripe Elements" requirement lines up with what the backend already produces. `Infrastructure/DependencyInjection.cs` only registers `IPaymentService` when `Stripe:SecretKey` is non-empty (currently `""` in `appsettings.json`) — this story does not change that; it's the same conditional-registration pattern already used for `IEmailService`/Redis.

### Why the payment amount is "cart total + shipping", not literally just "the cart total"

AC #1's literal text says "called with the cart total," but by checkout step 3 the customer has already chosen a shipping option (step 2, Story 4.4) whose cost is part of what they're actually paying — charging only the cart's item total would silently undercharge every order by its shipping cost. Cross-referencing Story 4.4's own AC #2 ("the order subtotal updates... to reflect the shipping cost") confirms "the total" is already understood, in this same epic, to include shipping — AC #1's wording here reads as shorthand from before the shipping mechanics were fully specified, not a deliberate decision to exclude it. This is a wording-vs-intent gap resolved by cross-referencing the surrounding epic's own established semantics (same class of resolution as Story 3.2's PostgreSQL-vs-SQL-Server AC conflict), not a business trade-off requiring escalation — no one benefits from an order that's silently short by its shipping cost.

### Why the shipping cost is looked up server-side, not trusted from the client

`CheckoutStore.shippingOption()` lives entirely client-side (Story 4.3's Dev Notes) — nothing stops a tampered request from sending an arbitrary `shippingOptionId` or, if the request shape allowed it, an arbitrary price directly. The request only ever carries an `id` (`"standard"`/`"express"`), and the handler resolves the actual `PriceInCents` server-side from `ShippingOptionsCatalog` — the same "never trust a client-sent price" principle already established for cart items (`CartService.AddItemAsync` always reads `Product.PriceInCents` from the database, never a client-supplied amount).

### Flutter mobile — same standing decision as Stories 4.3/4.4

See AC #8 and the linked persistent-memory decision (`epic4_flutter_checkout_gap`). Not re-litigated here.

### Established conventions this story must follow

- `CheckoutStore`: extend it in place (Task 6), same as Story 4.4 did — don't create a second checkout/payment store
- `CartStore`/`OrderStepIndicator`/`authGuard`: reuse directly, same as Stories 4.3/4.4
- Error-surfacing: don't repeat the silent-failure class of bug caught in both Story 4.3's and Story 4.4's reviews — `createPaymentIntent`'s failure path must set a readable error state from the start
- Step-order guard: Story 4.4's review added a redirect-if-a-prior-step-is-missing guard to `checkout-shipping` — this story's payment page needs the equivalent, checking both `address()` and `shippingOption()`

## Project Structure Notes

New `Application/Payments/` (Models, Commands) on the backend, parallel to `Application/Carts/`/`Application/Shipping/`. New `Web/Endpoints/Payments.cs`, parallel to `ShippingOptions.cs`. New `frontend/.../features/checkout/pages/checkout-payment/`, parallel to `checkout-address`/`checkout-shipping`.

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.5 acceptance criteria (Epic 4 section)
- `_bmad-output/implementation-artifacts/4-4-checkout-etape-2-choix-de-livraison.md` — the shipping-options catalog and step-order-guard pattern this story extends/reuses directly
- `backend/MonEcommerce/src/Application/Common/Interfaces/IPaymentService.cs`, `Infrastructure/ExternalServices/StripePaymentService.cs` — the existing Stripe integration this story wires up (built in Stories 1.5/1.6, not touched by this story beyond consuming it)
- `backend/MonEcommerce/src/Infrastructure/DependencyInjection.cs` — confirms `IPaymentService`'s conditional registration on `Stripe:SecretKey`, explaining why live end-to-end verification isn't possible here
- Stripe's official docs for `@stripe/stripe-js`, `Elements`, `PaymentElement`, and `confirmPayment` — no local precedent exists for any of this in the codebase, so the standard/documented API shapes are the reference

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 warnings, 0 errors. `dotnet test MonEcommerce.sln` — 168/168 passed (`Application.UnitTests`; `Domain.UnitTests`/`Infrastructure.IntegrationTests` report "no tests" — both projects have no test files, pre-existing and unrelated to this story).
- Frontend: `npx ng test --watch=false --browsers=ChromeHeadless` — 132/132 passed. `npm run build` (production SSR) — green, 12 static routes prerendered including `checkout/paiement` (guarded, prerenders fine — `authGuard` only runs at real navigation time, same as Stories 4.3/4.4).
- Reviewed the money-critical path specifically (server-side amount authority, auth guard): confirmed `CartDto.TotalInCents` and `IPaymentService.CreatePaymentIntentAsync(long, string, CancellationToken)` signatures match the handler's usage; confirmed `AuthorizationBehaviour` rejects unauthenticated requests before the handler runs, making `CreatePaymentIntentCommandHandler`'s `_user.Id!` safe; confirmed the shipping price is always resolved server-side via `ShippingOptionsCatalog.TryGetById`, never trusted from the client. No defects found — the implementation already reflects the error-surfacing and step-order-guard lessons from Stories 4.3/4.4's reviews (payment intent failures set a readable `paymentError`, Stripe.js load failures show a retry option, and the payment page redirects to whichever earlier step is missing).
- Explicitly NOT verified: an actual live payment against Stripe's sandbox — `Stripe:SecretKey`/`stripePublishableKey` are both empty in this environment (see Dev Notes), so `IPaymentService` isn't registered in DI and Stripe.js has no publishable key to load against. Verified via mocks/unit tests only, as scoped by Task 10.

### Completion Notes List

- Extracted `ShippingOptionsCatalog` as the single source of truth for shipping prices, shared between `GetShippingOptionsQueryHandler` (Story 4.4, listing) and `CreatePaymentIntentCommandHandler` (this story, authoritative pricing) — prevents the two from ever drifting apart.
- `CreatePaymentIntentCommandHandler` computes the charge as `cart.TotalInCents + shippingOption.PriceInCents`, resolving both values server-side (never trusting a client-sent amount or shipping id), and rejects an empty cart with a `ConflictException` rather than creating a nonsensical €0 payment intent.
- `CheckoutPaymentComponent` mounts Stripe's `PaymentElement` (not a custom card input, per AC #5) into an always-rendered container node (visibility toggled via `[hidden]`, not `@if`) specifically to avoid a `viewChild()`/async-mount race that would otherwise depend on Angular's change-detection timing.
- Applied the error-surfacing and step-order-guard conventions established by Stories 4.3/4.4's own review findings from the start, rather than needing a follow-up fix: `createPaymentIntent` sets a readable `paymentError` on failure (with a retry action in the UI), a failed `loadStripe()` shows its own retry path, and `ngOnInit` redirects to `/checkout/adresse` or `/checkout/livraison` if either prior step's state is missing.
- A decline (`confirmPayment`'s returned `error`) shows the AC's exact French copy and deliberately leaves `CartStore`/`CheckoutStore` untouched, so the customer can retry immediately without re-entering address/shipping.
- Flutter mobile out of scope, same standing decision as Stories 4.3/4.4 (AC #8).

### File List

**Backend**
- `backend/MonEcommerce/src/Application/Shipping/ShippingOptionsCatalog.cs` (new)
- `backend/MonEcommerce/src/Application/Shipping/Queries/GetShippingOptionsQueryHandler.cs` (modified — reads from the shared catalog)
- `backend/MonEcommerce/src/Application/Payments/Models/CreatePaymentIntentResponse.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/Commands/CreatePaymentIntentCommand.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/Commands/CreatePaymentIntentCommandValidator.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/Commands/CreatePaymentIntentCommandHandler.cs` (new)
- `backend/MonEcommerce/src/Web/Endpoints/Payments.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Payments/Commands/CreatePaymentIntentCommandHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Payments/Commands/CreatePaymentIntentCommandValidatorTests.cs` (new)

**Frontend**
- `frontend/mon-ecommerce-web/package.json`, `package-lock.json` (modified — `@stripe/stripe-js`)
- `frontend/mon-ecommerce-web/src/environments/environment.ts`, `environment.production.ts` (modified — `stripePublishableKey`)
- `frontend/mon-ecommerce-web/src/app/core/services/stripe-loader.service.ts` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.ts`, `checkout.store.spec.ts` (modified — `createPaymentIntent`, `paymentError`)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-payment/checkout-payment.component.ts`, `.html`, `.scss` (new)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-payment/checkout-payment.component.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — added `checkout/paiement` route, `authGuard`-protected)
