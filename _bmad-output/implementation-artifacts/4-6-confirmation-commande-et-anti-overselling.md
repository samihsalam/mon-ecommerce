# Story 4.6: Confirmation Commande & Anti-Overselling

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a customer,
I want to receive immediate order confirmation after payment with stock verification,
so that I have certainty my order is registered and the stock is reserved for me.

## Acceptance Criteria

1. **Given** Stripe sends a signed `payment_intent.succeeded` webhook, **when** `POST /api/v1/payments/webhook` processes it, **then** stock availability is checked atomically using EF Core optimistic concurrency. **[Resolved wording]**: `epics.md`/`architecture.md` say "`xmin` PostgreSQL" — this project is SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`, every migration to date). Same class of planning-doc drift as Story 3.2's PostgreSQL-vs-SQL-Server conflict, resolved the same way: use SQL Server's `rowversion` concurrency token instead. This is **not a new decision to make** — `Domain/Entities/Stock.cs` already has `public byte[] RowVersion` with the comment `// SQL Server rowversion concurrency token`, and `StockConfiguration.cs` already calls `.IsRowVersion()` on it. This story is the first to actually use that column for its intended purpose.
2. **Given** stock is insufficient at the time of webhook processing, **when** the overselling check fails, **then** a Stripe refund is issued automatically and a notification email is sent to the customer.
3. **Given** stock is sufficient, **when** the order is confirmed, **then** the order is created in the database, stock is decremented, and `OrderPlacedEvent` is published.
4. **Given** `OrderPlacedEvent` is published, **when** the `OrderPlacedEmailHandler` processes it, **then** a confirmation email with order number and summary is delivered within ≤ 30 seconds. **[Already built]**: `Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs` already exists and already does exactly this (French copy, order id, total) — this story only needs `OrderPlacedEvent` to actually get published with the right data; do not touch the handler.
5. The `OrderStepIndicator` shows "Étape 4/4 — Confirmée" on the confirmation page.
6. The confirmation page displays: order number, items summary, delivery address, estimated date.
7. The cart is cleared after successful order creation.
8. All payment transactions are logged in an audit trail (non-deletable).

## Tasks / Subtasks

### Backend — carrying checkout state through to the webhook (AC: #1, #3)

- [x] Task 1: Extend `CreatePaymentIntentCommand` (Story 4.5) to also receive and persist the shipping address
  - [x] The webhook fires asynchronously and by then has no access to the Angular `CheckoutStore` (client-side only, per Story 4.3's Dev Notes) — the only way it can know which address/cart/shipping option to build the `Order` from is data captured **at intent-creation time** and carried on the Stripe `PaymentIntent` itself
  - [x] `CreatePaymentIntentCommand` gains a `CheckoutAddress` parameter (`street`, `city`, `postalCode`, `country` — same shape as the frontend's `CheckoutAddress`); `CreatePaymentIntentCommandValidator` adds `NotEmpty()` on all four fields (mirror the frontend's `requiredNotBlank` intent — reject whitespace-only, not just empty)
  - [x] `CreatePaymentIntentCommandHandler`: persist a new `Address` row for `_user.Id` from the submitted fields (this is the "becomes a persisted `Address`/`Order.ShippingAddressId` reference... as part of placing the real order" moment Story 4.3's Dev Notes deferred to this story) — reuse `Address` exactly as it exists today, no schema change needed there
  - [x] Extend `IPaymentService.CreatePaymentIntentAsync` with an `IDictionary<string, string>? metadata = null` parameter; `StripePaymentService` passes it through as `PaymentIntentCreateOptions.Metadata`. The handler sets `{ "userId": ..., "shippingAddressId": ..., "shippingOptionId": ... }` — three GUIDs/strings, well under Stripe's 50-key/500-char metadata limits
  - [x] `Web/Endpoints/Payments.cs`'s `CreatePaymentIntentRequest` gains the address fields; `checkout-payment.component.ts`'s `loadPaymentForm()` now reads `this.checkoutStore.address()` (already set by Story 4.3 — the guard at the top of `ngOnInit` already ensures it's non-null) and passes it into `createPaymentIntent(...)`

### Backend — webhook signature verification (AC: #1)

- [x] Task 2: `IPaymentService.ConstructWebhookEvent(string json, string signatureHeader): Stripe.Event` (new method, same interface as `CreatePaymentIntentAsync`/`CreateRefundAsync` — no new abstraction needed, this is still "Stripe plumbing")
  - [x] `StripePaymentService` implements it via `Stripe.EventUtility.ConstructEvent(json, signatureHeader, webhookSecret)` — `webhookSecret` injected from `Stripe:WebhookSecret` (already an empty placeholder key in `appsettings.json`, same conditional-registration pattern as `Stripe:SecretKey`)
  - [x] An invalid/unverifiable signature throws Stripe.net's own `StripeException` — let the endpoint catch it and return `400`, never process an unverified payload

### Backend — anti-overselling core logic (AC: #1, #2, #3, #7, #8)

- [x] Task 3 (superseded during review — see Review Findings): originally planned as `Domain/Exceptions/InsufficientStockException.cs` (per `architecture.md`'s source tree); the actual implementation uses `HandleStripeWebhookCommandHandler.TryReserveStockAsync(...): Task<bool>` instead — a `false` return means insufficient stock (whether from the initial check or from retry exhaustion), functionally equivalent to a thrown exception but avoiding exceptions for expected, non-exceptional control flow. The file was created per this task's original text, found genuinely unused anywhere, and deleted during review.
- [x] Task 4: `Domain/Events/StockUnavailableEvent.cs` — **do NOT reuse `RefundIssuedEvent`** (Domain/Events/RefundIssuedEvent.cs already exists, used by `Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs` for Epic 5's post-delivery return flow). That handler's email hardcodes `"...a été émis pour la commande {OrderId}"` — in this story's refund path **no `Order` is ever created** (AC #3 only creates the order when stock is sufficient), so there is no `OrderId` to reference; reusing it would send a nonsensical email pointing at a fake/empty order. New event: `record StockUnavailableEvent(string CustomerEmail, int AmountInCents) : BaseEvent` + `Application/Payments/EventHandlers/StockUnavailableEmailHandler.cs` (French copy: payment was refunded because one or more items are no longer available — AC #2's exact intent, not a return/order reference)
- [x] Task 5: `ICartService.ClearCartAsync(CartOwner owner, CancellationToken ct = default): Task` — new method (doesn't exist today), called after order creation succeeds (AC #7)
- [x] Task 6: `Domain/Entities/PaymentAuditLog.cs` (new, `BaseEntity` — not `BaseAuditableEntity`, since "non-deletable" means no code path ever calls `Remove()` on it, and it needs no `LastModified` since it's never updated after insert): `StripePaymentIntentId`, `UserId`, `AmountInCents`, `Outcome` (enum or string: `Confirmed` / `Refunded`), `OrderId` (nullable — only set on the `Confirmed` outcome), `Created` (inherited from `BaseEntity`). New `PaymentAuditLogConfiguration.cs`. "Non-deletable" is enforced at the application layer: no command/handler anywhere ever deletes a `PaymentAuditLog` row — do not add a cascade delete from `Order` or `ApplicationUser` that would remove it
- [x] Task 7: `Domain/Entities/Order.cs` gains `StripePaymentIntentId` (string) — needed so the frontend confirmation page (Task 12) can poll for the order by payment intent id, and so the audit log can cross-reference it. New migration (EF Core tooling per Story 4.1/4.3's established `EFCoreToolsRunning=true` + design-time-factory fix)
- [x] Task 8: `Application/Payments/Webhooks/HandleStripeWebhookCommand.cs` (matches `architecture.md`'s planned `Webhooks/HandleStripeWebhook` location) + Handler — the actual confirmation logic:
  - [x] Parse the verified `Stripe.Event`; if `event.Type != "payment_intent.succeeded"`, return `200 OK` immediately and do nothing (Stripe sends many event types to the same webhook URL; only this one is relevant here — ignoring others is correct, not a gap)
  - [x] Extract `userId`, `shippingAddressId`, `shippingOptionId` from the `PaymentIntent`'s `Metadata` (set in Task 1); resolve the shipping price via `ShippingOptionsCatalog.TryGetById` (same server-side-price-authority principle as `CreatePaymentIntentCommandHandler`)
  - [x] Load the user's cart via `ICartService.GetCartAsync`; if empty, treat as already-processed/no-op (idempotency: Stripe retries webhooks on non-2xx responses or timeouts — a second delivery of the same `payment_intent.succeeded` must not double-create an order or double-decrement stock). **Idempotency guard**: before doing anything else, check whether a `PaymentAuditLog` row already exists for this `StripePaymentIntentId` — if so, return `200 OK` immediately without reprocessing
  - [x] For each cart item's `Stock` row: load it, check `Quantity >= item.Quantity` — if not, return `false` from `TryReserveStockAsync` (see Task 3 note) before decrementing anything. If sufficient, decrement `Quantity -= item.Quantity`
  - [x] Call `SaveChangesAsync` once for all decremented `Stock` rows together; on `DbUpdateConcurrencyException` (another request changed a `Stock` row's `RowVersion` between this handler's read and write), reload **every** tracked `Stock` row (not just the ones the exception reports as conflicted — see Review Findings #1) and re-run the sufficiency check + decrement — bounded retry (3 attempts) before giving up and returning `false` (a real, if rare, oversell under heavy concurrent load, not a bug to swallow silently)
  - [x] **On success**: create the `Order` (`Status = OrderStatus.Pending`, `Items` from cart, `ShippingAddressId`, `TotalInCents = cart.TotalInCents + shippingOption.PriceInCents`, `StripePaymentIntentId`), insert a `PaymentAuditLog` row (`Outcome = Confirmed`, `OrderId` set), call `ICartService.ClearCartAsync`, publish `OrderPlacedEvent(order.Id, userId, customerEmail, order.TotalInCents)` (reuses the existing event/handler from Task 4's note — AC #4 needs no new code)
  - [x] **On insufficient stock, or on a cart/charge-amount mismatch (Review Findings #3)**: call `IPaymentService.CreateRefundAsync(paymentIntentId)`, insert a `PaymentAuditLog` row (`Outcome = Refunded`, `OrderId = null`), publish `StockUnavailableEvent` (Task 4) — do NOT create an `Order`, do NOT clear the cart (the customer still has it to retry with adjusted quantities). An **empty** cart at this point is a no-op instead (Review Findings #2), not a refund trigger.
  - [x] Resolve the customer's email via `IUser`/`IIdentityService` (same pattern already used elsewhere — `CreatePaymentIntentCommandHandler` already resolves the current user this way for the authenticated create-intent call; the webhook itself is unauthenticated, so the email must come from the `userId` in the PaymentIntent metadata, not from an `IUser` claims principal — there is no HTTP-authenticated caller for a Stripe-initiated webhook request)
- [x] Task 9: `Web/Endpoints/Payments.cs` — add `POST /webhook`, **no `.RequireAuthorization()`** (Stripe calls this directly, authenticated only by its own signature, not a JWT) — read the raw request body as a string (needed for signature verification — do not let model binding parse it as JSON first, the exact raw bytes matter for the signature check) and the `Stripe-Signature` header
- [x] Task 10: `Application/Orders/Queries/GetOrderByPaymentIntentQuery.cs` + Handler + `Web/Endpoints/Orders.cs` (new, or add to an existing orders endpoint group if one exists) — `GET /api/v1/orders/by-payment-intent/{paymentIntentId}`, `[Authorize]`, returns `404` while the webhook hasn't landed yet (frontend polls — Task 12), the order summary (id, items, shipping address, total, created date) once `Order.StripePaymentIntentId` matches, or a `409`-with-message shape if a matching `PaymentAuditLog.Outcome == Refunded` row exists instead (so the frontend can show "your card was not charged / stock ran out" rather than polling forever)
  - [x] **Scope the lookup to `_user.Id` in the query itself** (`WHERE Order.UserId == _user.Id`, same for the `PaymentAuditLog` check) — `[Authorize]` alone only proves the caller is authenticated as *someone*, not that they own this specific `paymentIntentId`. Stripe's `pi_...` ids aren't meant to double as an authorization credential; treat this the same as every other per-user resource lookup already in the codebase (e.g. `GetOrderDetailQueryHandler` in `Application/Account/Queries/`) and return `404` (not `403`) for a mismatched owner, to avoid confirming the id exists at all
- [x] Task 11: Backend tests
  - [x] `CreatePaymentIntentCommandHandlerTests`/`Validator` additions for the new address fields
  - [x] `HandleStripeWebhookCommandHandlerTests`: creates order + decrements stock + publishes `OrderPlacedEvent` + clears cart on sufficient stock; issues a refund + publishes `StockUnavailableEvent` + creates no order on insufficient stock; is idempotent on a duplicate `payment_intent.succeeded` delivery (second call is a no-op, verified via the `PaymentAuditLog` guard); ignores non-`payment_intent.succeeded` event types
  - [x] A concurrency-conflict test: two simulated concurrent decrements of the same `Stock` row, only one should win the last unit, the other should see the retry-then-insufficient-stock path (this is the one AC #1 is actually testing). **Note (Review Findings #6)**: the test added, `StockRowVersion_ShouldCauseAConcurrencyExceptionWhenTwoContextsRaceOnTheSameRow`, proves the underlying EF `DbUpdateConcurrencyException` mechanism fires correctly on a real `RowVersion` mismatch, but does not drive it through `TryReserveStockAsync`'s own retry loop — that would require interleaving a second writer inside the handler's single synchronous read-decrement-save call, which has no externally-controllable await point in a sequential test. Accepted, documented gap, same as the Stripe-key-unavailable gap elsewhere in this story.
  - [x] `PaymentsEndpointTests` (or equivalent integration-style test) for signature rejection on a tampered/invalid `Stripe-Signature`

### Frontend — Angular only

- [x] Task 12: `checkout-payment.component.ts`'s `loadPaymentForm()` passes `this.checkoutStore.address()` into `createPaymentIntent(shippingOptionId, address)` (extend `CheckoutStore.createPaymentIntent`'s signature and its `POST` body to include the address fields — matches Task 1's backend change)
- [x] Task 13: New `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-confirmation/checkout-confirmation.component.ts` (parallel naming to `checkout-address`/`checkout-shipping`/`checkout-payment`)
  - [x] On init: read the Stripe `payment_intent` id from the URL (Stripe's `redirect: 'if_required'` flow from Story 4.5 stays on `/checkout/paiement` for the common case and calls `router.navigate(['/checkout/confirmation'])` directly — pass the `PaymentIntent.Id` as a route param or query param at that navigation call, e.g. `/checkout/confirmation?payment_intent=pi_...`, since Stripe also appends this same param automatically for payment methods that DO redirect off-site, so both paths converge on the same shape)
  - [x] Poll `GET /api/v1/orders/by-payment-intent/{id}` (Task 10) every ~2s (bounded — e.g. give up after ~30s given AC #4's own ≤30s email SLA as the natural upper bound, matching how long confirmation is expected to take) until it returns the order (show it) or the refunded-conflict shape (show a clear "stock unavailable, you were refunded" message, cart is intentionally still intact so the customer can adjust and retry) or the poll window expires (show a "still processing, check your email" fallback rather than an infinite spinner)
  - [x] Render: order number, items summary, delivery address, estimated date (reuse `checkout-shipping`'s already-selected option's `estimatedDelay` text, carried via `CheckoutStore.shippingOption()` still being set at this point — no new backend field needed for this)
  - [x] Mount `<app-order-step-indicator [currentStep]="4" />` (AC #5)
  - [x] On successful render, nothing further to do with `CheckoutStore`/`CartStore` — the backend already cleared the cart (AC #7); do not also clear it client-side and risk double-logic drifting from the server's own source of truth
- [x] Task 14: New route `checkout/confirmation` in `app.routes.ts`, `canActivate: [authGuard]` — same reasoning as Stories 4.3–4.5
- [x] Task 15: Frontend tests
  - [x] `checkout-payment.component.spec.ts` addition: `createPaymentIntent` is called with the address alongside the shipping option id
  - [x] `checkout-confirmation.component.spec.ts`: renders the order summary once the poll succeeds; shows the refunded/stock-unavailable message on the `409` shape; shows the fallback message if the poll window expires without a result

### Verification

- [x] Task 16: Full verification
  - [x] Backend: `dotnet build` + `dotnet test` green
  - [x] Frontend: `npm run build` (production SSR) + `npm test` green
  - [x] Explicitly NOT verified: an actual live Stripe webhook delivery (no test-mode keys/webhook secret configured in this environment, same gap as Story 4.5) — covered by handler-level unit tests constructing a `Stripe.Event` payload directly, not a live Stripe CLI `stripe trigger` run

## Dev Notes

### Why "xmin PostgreSQL" in the AC doesn't block this story

See AC #1's resolved-wording note. `architecture.md` itself is internally inconsistent on this point — line ~181 already says "concurrence optimiste EF Core (**RowVersion**)" in its high-level tech-decision summary, and only the detailed tech-decision table (line ~201) adds the stale "`xmin` PostgreSQL" parenthetical. The schema already committed to `RowVersion` (see `Stock.cs`/`StockConfiguration.cs`, built ahead of this story). This is resolved by evidence already in the repository, not a call needing escalation — same class of resolution as Story 3.2's own PostgreSQL/SQL-Server conflict (cited in Story 4.5's Dev Notes as established precedent).

### Why the address has to move from the frontend-only `CheckoutStore` into a real backend call now

Stories 4.3–4.5 deliberately kept the address/shipping selection as pure client-side `CheckoutStore` state, with Story 4.3's Dev Notes explicitly flagging that persisting a real `Address` row and setting `Order.ShippingAddressId` was deferred to "Story 4.6... as part of placing the real order." This story is where that deferral resolves: the address has to exist server-side, reachable by an asynchronous webhook that runs after the browser session that submitted it may already be gone, which is why it's threaded through Stripe's own `PaymentIntent.Metadata` rather than, say, a new session/cache entry.

### Why a new `StockUnavailableEvent` instead of reusing `RefundIssuedEvent`

See Task 4. `RefundIssuedEvent`/`RefundIssuedEmailHandler` already exist and are wired for Epic 5's post-delivery-return refund flow, where a real `Order` already exists at refund time. This story's refund happens *before* any `Order` is ever created (that's the entire point of checking stock before creating one) — there is no `OrderId` to put in that event without inventing a fake one, and the existing handler's email copy ("...pour la commande {OrderId}") would be actively wrong here. Building a second, narrower event for this specific pre-order refund case is the correct call, not overengineering — the two refund scenarios have genuinely different available data and customer-facing meaning.

### Idempotency is not optional

Stripe redelivers webhooks on any non-2xx response or timeout. Without a duplicate-delivery guard, a slow response (e.g. under load) could cause Stripe to retry `payment_intent.succeeded` for the same payment, which would double-decrement stock and/or create two orders for one payment if not guarded. The `PaymentAuditLog` existence check (Task 8) is this guard — it is also required anyway for AC #8's audit trail, so it carries no extra schema cost.

**Known, accepted limitation**: the idempotency check (`PaymentAuditLogs.AnyAsync(...)` before processing) is a read-then-write check, not enforced by a unique constraint — `PaymentAuditLogConfiguration`'s index on `StripePaymentIntentId` is deliberately non-unique (a duplicate delivery for the same intent is the expected case the guard checks for, not something the schema should reject outright). This leaves a narrow TOCTOU window: two genuinely *concurrent* deliveries of the same event could both pass the check before either writes its audit row. In practice Stripe's own retry behavior is sequential (a retry is only sent after a prior attempt's response/timeout), so true concurrent duplicate delivery is very unlikely, not eliminated. Same class of accepted, documented-not-solved gap as `AccountService.UpdateProfileAsync`'s email-change TOCTOU (Story 2.1) — tracked here rather than adding a unique-constraint-plus-catch guard that this story's scope doesn't otherwise call for.

### Flutter mobile — same standing decision as Stories 4.3–4.5

Not re-litigated here; see the linked persistent-memory decision (`epic4_flutter_checkout_gap`).

## Project Structure Notes

New `Application/Payments/Webhooks/`, `Application/Payments/EventHandlers/`, new `PaymentAuditLog` entity/configuration/migration, new `Order.StripePaymentIntentId` migration. New `frontend/.../features/checkout/pages/checkout-confirmation/`, parallel to the existing three checkout pages. Matches `architecture.md`'s planned source tree (`Webhooks/HandleStripeWebhook`) with two deviations: the one addition (`PaymentAuditLog`) that tree didn't explicitly list but AC #8 requires, and one planned-but-unused file (`Domain/Exceptions/InsufficientStockException.cs`, created per the original task text then deleted during review — see Review Findings #6 — since the actual implementation uses a bool-returning helper instead).

## References

- `_bmad-output/planning-artifacts/epics.md` — Story 4.6 acceptance criteria (Epic 4 section, line ~858)
- `_bmad-output/planning-artifacts/architecture.md` — anti-overselling tech decision (lines ~181, ~201), planned source tree (lines ~460–485), Stripe webhook boundary (line ~572)
- `_bmad-output/implementation-artifacts/4-3-checkout-etape-1-adresse-de-livraison.md` — Dev Notes deferring `Address` persistence to this story
- `_bmad-output/implementation-artifacts/4-5-checkout-etape-3-paiement-stripe.md` — the `CreatePaymentIntentCommand`/`IPaymentService`/`ShippingOptionsCatalog` this story extends
- `backend/MonEcommerce/src/Domain/Entities/Stock.cs`, `Infrastructure/Data/Configurations/StockConfiguration.cs` — the already-built `RowVersion` concurrency token this story is the first to actually use
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs` — already built, reused as-is (AC #4)
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs`, `Domain/Events/RefundIssuedEvent.cs` — the existing but wrongly-shaped-for-this-case event this story must NOT reuse (see Dev Notes)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/StripePaymentService.cs`, `Application/Common/Interfaces/IPaymentService.cs` — extended, not replaced
- `backend/MonEcommerce/src/Web/appsettings.json` — `Stripe:WebhookSecret` already present as an empty placeholder

## Review Findings

Three-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor — parallel background agents against the full diff) ran after initial implementation. All fixes below were applied and re-verified (`dotnet test`: 186/186; `ng test`: 139/139).

1. **Fixed — stock double-decrement on concurrency retry.** `TryReserveStockAsync`'s original catch block only reloaded the `Stock` rows EF's `DbUpdateConcurrencyException.Entries` reported as actually conflicted. Because EF's change tracker hands back the SAME tracked instances on the next loop iteration (identity map), any row that did NOT conflict still held that attempt's already-applied in-memory decrement — and got decremented a second time on retry, silently over-subtracting stock that was never sold. Fixed by reloading every tracked row in the batch on any conflict, not just the reported ones (required adding `EntityEntry<TEntity> Entry<TEntity>(...)` to `IApplicationDbContext` — `ApplicationDbContext` already satisfies it via the base `DbContext`).
2. **Fixed — empty cart wrongly triggered a refund.** The original code treated `cart.Items.Count == 0` identically to insufficient stock: real Stripe refund + `StockUnavailableEvent` + misleading "items unavailable" email, contradicting this story's own Task 8 spec ("if empty, treat as already-processed/no-op"). Split into its own branch: an empty cart at this point (given the idempotency guard already ran) is now a silent no-op, not a refund trigger.
3. **Fixed — cart could drift between PaymentIntent creation and webhook delivery.** Only `userId`/`shippingAddressId`/`shippingOptionId` are threaded through Stripe metadata, not a cart snapshot; the webhook reads the LIVE cart. If the customer edited their cart in another tab/device between confirming payment and the webhook firing, the resulting Order's items could silently mismatch what was actually charged. Mitigated (not eliminated — see Known limitations) by comparing `cart.TotalInCents + shippingOption.PriceInCents` against Stripe's actually-charged amount before reserving stock; a mismatch now refunds and notifies instead of silently creating a wrong order.
4. **Fixed — idempotency TOCTOU could leak reserved stock.** Two genuinely concurrent deliveries of the same webhook event could both pass the `PaymentAuditLogs.AnyAsync` pre-check before either commits, both reserve stock, and then race on the Order insert (unique on `StripePaymentIntentId`). The loser previously had no handling for that `DbUpdateException`, so its already-decremented stock was never given back. Fixed by catching the unique-constraint failure on the final save and releasing the loser's reserved stock — same recovery pattern already established in `CartService.FindOrCreateActiveCartAsync`'s split-cart race, not a new technique for this codebase.
5. **Fixed — confirmation page kept polling after navigation away.** `checkout-confirmation.component.ts` had no destroy handling; its `for`-loop poll continued firing HTTP requests (and writing to signals on a detached component) for up to ~30s after the customer navigated elsewhere. Fixed with a `DestroyRef.onDestroy` guard checked at each poll step.
6. **Documented, not fixed — concurrency-retry path lacks a true interleaved test.** See Task 11's inline note. Accepted: reproducing real interleaving inside a sequential unit test isn't possible without an externally-controllable await point the handler doesn't have; the underlying EF mechanism the retry depends on is verified separately.
7. **Documented, not fixed — retry exhaustion (3 attempts) can refund a customer who would have succeeded on a 4th attempt** under a flash-sale-style burst of concurrent webhook deliveries for the same product. A tuning tradeoff, not a correctness bug; 3 was already a deliberate bound per the original design, not revisited here.
8. **Cosmetic — stale comments** referencing a `ProcessPaymentWebhookCommandHandler` class that was never actually named that (always `HandleStripeWebhookCommandHandler`) — fixed in `PaymentAuditLog.cs`, `CartService.cs`, `PaymentAuditLogConfiguration.cs`.
9. **Not changed — `PaymentAuditLog` uses `BaseAuditableEntity`, not the plain `BaseEntity` Task 6 originally specified.** Harmless (two unused columns, `LastModified`/`LastModifiedBy`); changing it now would require another migration for no behavioral gain, so left as-is.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Backend: `dotnet build MonEcommerce.sln` — 0 warnings, 0 errors. `dotnet test MonEcommerce.sln` — 186/186 passed after review fixes (184 baseline + 2 new tests for the empty-cart no-op and cart/charge-amount mismatch guards; `Application.UnitTests`; `Domain.UnitTests`/`Infrastructure.IntegrationTests` report "no tests" — pre-existing, both projects have no test files, unrelated to this story).
- Frontend: `npx ng test --watch=false --browsers=ChromeHeadless` — 139/139 passed. `npm run build` (production SSR) — green, 13 static routes prerendered including `checkout/confirmation`.
- Local environment note: this machine only has the .NET 10 SDK installed, while `global.json` pins `9.0.101`. Verified builds/tests by temporarily setting `rollForward` to `latestMajor` (never committed — reverted before finalizing); the actual target framework (`net9.0`) is unaffected since the 9.0.17 runtime is present, only the SDK pinning needed the temporary override.
- Fixed a genuine template bug found during verification: `checkout-confirmation.component.html` originally used `@else if (order(); as confirmedOrder)` — Angular's `as` binding is only valid on the primary `@if`, not `@else if`; this failed to even JIT-compile in tests. Fixed with `@else if (order())` + `@let confirmedOrder = order()!;` inside the block (Angular 19's `@let`).
- Fixed two backend unit-test issues while writing `HandleStripeWebhookCommandHandlerTests`: (1) `OrderPlacedEvent` is raised via `order.AddDomainEvent(...)` and only reaches a subscriber through `DispatchDomainEventsInterceptor`, which needed to be wired into the test's `DbContextOptionsBuilder` (a bare in-memory context has no interceptors) — also required verifying via `It.Is<BaseEvent>(...)` rather than `It.Is<OrderPlacedEvent>(...)`, since the interceptor's loop variable is statically typed `BaseEvent`, and Moq matches on the generic type argument actually used at the call site, not the runtime type. (2) EF Core's InMemory provider doesn't auto-regenerate `rowversion`-style concurrency tokens the way SQL Server does on every `UPDATE`, so the concurrency-conflict test manually reassigns `Stock.RowVersion` before the "winning" context's save, standing in for what SQL Server does automatically — isolates exactly what the test cares about (does EF's concurrency-token comparison throw on a mismatch) without depending on InMemory behavior it doesn't have.
- Scoped down one test from the original plan: a fully realistic interleaving of the webhook handler's own retry loop under real concurrency (two requests racing inside a single `Handle()` call) isn't reproducible from a sequential unit test, since the handler has no externally-controllable await point between its read and its save. Covered instead by (a) the handler-level success/insufficient-stock/idempotency tests using its real retry-capable code path, and (b) a lower-level test proving the underlying EF concurrency-token mechanism the retry loop depends on actually throws `DbUpdateConcurrencyException` when two contexts race on the same `Stock` row.

### Completion Notes List

- Backend: extended `CreatePaymentIntentCommand`/Handler (Story 4.5) to persist the shipping address at intent-creation time and carry `userId`/`shippingAddressId`/`shippingOptionId` on the Stripe `PaymentIntent`'s metadata — the only channel available to the asynchronous, unauthenticated webhook that later confirms the order.
- Added `IPaymentService.ParseWebhookEvent` (Stripe signature verification via `EventUtility.ConstructEvent`, rethrown as `InvalidWebhookSignatureException` → 400) and `HandleStripeWebhookCommand`/Handler: verifies the signature, ignores non-`payment_intent.succeeded` events, guards against duplicate delivery via `PaymentAuditLog`, reserves stock with a bounded (3-attempt) retry loop against `Stock.RowVersion` concurrency conflicts, and on success creates the `Order`+`OrderItem`s, decrements stock, writes the audit log, publishes `OrderPlacedEvent` (reusing the already-built `OrderPlacedEmailHandler` from Story 1.x untouched), and clears the cart. On insufficient stock: issues a Stripe refund, writes the audit log, and publishes a new `StockUnavailableEvent`/`StockUnavailableEmailHandler` — deliberately not the pre-existing `RefundIssuedEvent`, which assumes an `Order` already exists (Epic 5's return-refund flow) and would have sent a nonsensical email referencing a nonexistent order.
- Added `GetOrderByPaymentIntentQuery`/`AccountService.GetOrderByPaymentIntentAsync` (`GET /api/v1/account/orders/by-payment-intent/{paymentIntentId}`), scoped to the requesting user in the query itself (not just `[Authorize]`) — returns the order once confirmed, `409` if a `PaymentAuditLog` shows the payment was refunded for insufficient stock, `404` while still pending.
- New migration `AddPaymentAuditLogAndOrderPaymentIntent`: `PaymentAuditLog` table (non-deletable — no code path anywhere calls `Remove()` on it) and `Order.StripePaymentIntentId` (unique, filtered on non-null).
- Frontend: extended `CheckoutStore.createPaymentIntent` to send the address, and `checkout-payment.component.ts`'s `onSubmit` to pass `payment_intent` as a query param on the confirmation-page navigation (matching Stripe's own redirect contract for payment methods that need one). New `checkout-confirmation.component.ts`: polls `getOrderByPaymentIntent` every ~2s for up to ~30s (matching AC #4's own email SLA as the natural upper bound), rendering the order summary, delivery address, and estimated delay on success, a clear message on the refunded-for-insufficient-stock outcome, and a "still processing, check your email" fallback if the poll window expires — never an infinite spinner.
- Flutter mobile out of scope, same standing decision as Stories 4.3–4.5 (AC #8's original numbering — see epics.md).

### File List

**Backend**
- `backend/MonEcommerce/src/Application/Payments/Commands/CreatePaymentIntentCommand.cs`, `CreatePaymentIntentCommandHandler.cs`, `CreatePaymentIntentCommandValidator.cs` (modified — address fields, metadata)
- `backend/MonEcommerce/src/Application/Common/Interfaces/IPaymentService.cs`, `IApplicationDbContext.cs`, `ICartService.cs`, `IIdentityService.cs`, `IAccountService.cs` (modified — new methods/DbSet for this story)
- `backend/MonEcommerce/src/Application/Common/Exceptions/InvalidWebhookSignatureException.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/Models/WebhookEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/Webhooks/HandleStripeWebhookCommand.cs`, `HandleStripeWebhookCommandHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Payments/EventHandlers/StockUnavailableEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Account/Queries/GetOrderByPaymentIntentQuery.cs`, `GetOrderByPaymentIntentQueryHandler.cs` (new)
- `backend/MonEcommerce/src/Domain/Entities/Order.cs` (modified — `StripePaymentIntentId`), `PaymentAuditLog.cs` (new)
- `backend/MonEcommerce/src/Domain/Enums/PaymentAuditOutcome.cs` (new)
- `backend/MonEcommerce/src/Domain/Events/StockUnavailableEvent.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/ExternalServices/StripePaymentService.cs` (modified — metadata, `ParseWebhookEvent`)
- `backend/MonEcommerce/src/Infrastructure/Carts/CartService.cs` (modified — `ClearCartAsync`)
- `backend/MonEcommerce/src/Infrastructure/Identity/AccountService.cs`, `IdentityService.cs` (modified — `GetOrderByPaymentIntentAsync`, `GetEmailAsync`)
- `backend/MonEcommerce/src/Infrastructure/Data/Configurations/OrderConfiguration.cs` (modified), `PaymentAuditLogConfiguration.cs` (new)
- `backend/MonEcommerce/src/Infrastructure/Data/Migrations/20260728230451_AddPaymentAuditLogAndOrderPaymentIntent.cs` + `.Designer.cs`, `ApplicationDbContextModelSnapshot.cs` (new/modified)
- `backend/MonEcommerce/src/Web/Endpoints/Payments.cs` (modified — address fields, `/webhook`), `Account.cs` (modified — `orders/by-payment-intent/{id}`)
- `backend/MonEcommerce/src/Web/Infrastructure/ProblemDetailsExceptionHandler.cs` (modified — `InvalidWebhookSignatureException` → 400)
- `backend/MonEcommerce/tests/Application.UnitTests/Payments/Commands/CreatePaymentIntentCommandHandlerTests.cs`, `CreatePaymentIntentCommandValidatorTests.cs` (modified)
- `backend/MonEcommerce/tests/Application.UnitTests/Payments/Webhooks/HandleStripeWebhookCommandHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Account/Services/AccountServiceOrdersTests.cs` (modified), `Account/AuthorizationPipelineTests.cs` (modified — stub interfaces updated for new methods)

**Frontend**
- `frontend/mon-ecommerce-web/src/app/features/checkout/checkout.store.ts`, `checkout.store.spec.ts` (modified — address on `createPaymentIntent`, `getOrderByPaymentIntent`)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-payment/checkout-payment.component.ts`, `.spec.ts` (modified — pass address, navigate with `payment_intent` query param)
- `frontend/mon-ecommerce-web/src/app/features/checkout/pages/checkout-confirmation/checkout-confirmation.component.ts`, `.html`, `.scss`, `.spec.ts` (new)
- `frontend/mon-ecommerce-web/src/app/app.routes.ts` (modified — added `checkout/confirmation` route, `authGuard`-protected)
