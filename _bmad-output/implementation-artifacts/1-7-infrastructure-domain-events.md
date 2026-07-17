# Story 1.7: Infrastructure Domain Events

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want MediatR Notifications infrastructure with `OrderPlacedEvent`, `OrderShippedEvent`, `ReturnRequestedEvent`, `RefundIssuedEvent` and their email handlers wired up,
so that all transactional emails fire automatically from domain events without coupling business logic to email sending.

## Acceptance Criteria

1. **Given** an `OrderPlacedEvent` is published via `IMediator.Publish()`, **when** the handler processes it, **then** `IEmailService.SendAsync` is called with the correct order confirmation data (recipient, subject, body containing order reference and total).
2. **Given** an `OrderShippedEvent` is published, **when** the handler processes it, **then** the shipment notification email is dispatched (handler completes without throwing; ≤ 30s is a runtime/production SLA, not something a unit test times — cover it with a comment/TODO for the future integration test in Story 5.4).
3. **Given** a `ReturnRequestedEvent` is published, **when** the handler processes it, **then** an acknowledgement email is sent via `IEmailService.SendAsync`.
4. **Given** a `RefundIssuedEvent` is published, **when** the handler processes it, **then** a refund confirmation email is sent via `IEmailService.SendAsync` containing the refunded amount.
5. All four events are immutable C# `record` types deriving from `BaseEvent`, with required payload fields (no optional/nullable fields unless truly optional business data).
6. Event handler naming follows `{Event}{Action}Handler` convention (e.g. `OrderPlacedEmailHandler`).
7. Handler failures (e.g. `IEmailService.SendAsync` throws) are caught inside the handler, logged via `ILogger<T>` at `LogError` level, and **do not** propagate to the caller (fire-and-forget pattern — `_mediator.Publish` in `DispatchDomainEventsInterceptor` must not fail the `SaveChanges` call because an email provider is down).
8. Unit tests (NUnit + Moq, in `Application.UnitTests`) cover each of the 4 handlers in isolation: one "happy path" test asserting `IEmailService.SendAsync` was called once with expected arguments, and one "email service throws" test asserting the exception is swallowed and logged, not rethrown.

## Tasks / Subtasks

- [x] Task 1: Create the four domain event records (AC: #5, #6)
  - [x] `src/Domain/Events/OrderPlacedEvent.cs` — `public record OrderPlacedEvent(Guid OrderId, string UserId, string CustomerEmail, int TotalInCents) : BaseEvent;`
  - [x] `src/Domain/Events/OrderShippedEvent.cs` — `public record OrderShippedEvent(Guid OrderId, string CustomerEmail, string TrackingNumber) : BaseEvent;`
  - [x] `src/Domain/Events/ReturnRequestedEvent.cs` — `public record ReturnRequestedEvent(Guid ReturnId, Guid OrderId, string CustomerEmail, string Reason) : BaseEvent;`
  - [x] `src/Domain/Events/RefundIssuedEvent.cs` — `public record RefundIssuedEvent(Guid RefundId, Guid OrderId, string CustomerEmail, int AmountInCents) : BaseEvent;`
  - [x] Note: `Guid ReturnId`/`Guid RefundId` are forward-looking identifiers — the `ReturnRequest`/`Refund` domain entities do not exist yet (they land in Epic 5). Keep the event payloads self-contained (primitive fields only, no entity references) so this story has zero dependency on unbuilt Epic 5 entities.
- [x] Task 2: Create the email handlers (AC: #1, #2, #3, #4, #6, #7)
  - [x] `src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs` — `INotificationHandler<OrderPlacedEvent>`
  - [x] `src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs` — `INotificationHandler<OrderShippedEvent>`
  - [x] `src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` — `INotificationHandler<ReturnRequestedEvent>`
  - [x] `src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs` — `INotificationHandler<RefundIssuedEvent>`
  - [x] Each handler constructor takes `IEmailService` + `ILogger<THandler>`, wraps the `SendAsync` call in `try/catch`, and logs+swallows on failure (see Dev Notes for the exact pattern to copy).
- [x] Task 3: Unit tests (AC: #8)
  - [x] `tests/Application.UnitTests/Orders/EventHandlers/OrderPlacedEmailHandlerTests.cs`
  - [x] `tests/Application.UnitTests/Orders/EventHandlers/OrderShippedEmailHandlerTests.cs`
  - [x] `tests/Application.UnitTests/Returns/EventHandlers/ReturnRequestedEmailHandlerTests.cs`
  - [x] `tests/Application.UnitTests/Returns/EventHandlers/RefundIssuedEmailHandlerTests.cs`
  - [x] Each test file: happy-path (`SendAsync` called once, verify args) + failure-swallowed (`SendAsync` throws, `Handle()` still completes without throwing, logger `LogError` called once).
- [x] Task 4: Build/verify wiring (no new DI registration needed — see Dev Notes)
  - [x] `dotnet build` from `backend/MonEcommerce/` succeeds
  - [x] `dotnet test` from `backend/MonEcommerce/` — all existing + new tests pass

### Review Findings

- [x] [Review][Defer] `BaseEvent` class→record conversion changes domain-event equality from reference- to value-equality — `BaseEntity.RemoveDomainEvent` (`src/Domain/Common/BaseEntity.cs:23`) now uses structural `Equals` for every future derived event, so `List<T>.Remove` could remove the wrong instance if two value-identical events are ever queued. The class→record conversion itself was unavoidable (C# forbids `record : class`, and AC #5 requires the 4 events to be `record` types); the open design question (distinguishing `EventId`, or reference-equality override) is deferred — not reachable by any current code (no caller of `AddDomainEvent`/`RemoveDomainEvent` exists yet). Revisit when the first Epic 4/5 story wires a real entity to raise these events.
- [x] [Review][Patch] Happy-path tests don't verify email content, only recipient — all 4 `*EmailHandlerTests.cs` use `It.IsAny<string>()` for `subject`/`htmlBody`, so AC #1/#3/#4's requirement that the body contain order total/refund amount/tracking number/reason is never actually asserted [tests/Application.UnitTests/Orders/EventHandlers/OrderPlacedEmailHandlerTests.cs, tests/Application.UnitTests/Returns/EventHandlers/RefundIssuedEmailHandlerTests.cs] — fixed: all 4 happy-path tests now assert `htmlBody` contains the relevant order id/amount/tracking number/reason via `It.Is<string>(...)`
- [x] [Review][Patch] Unescaped free-text (`Reason`, `TrackingNumber`) interpolated directly into an HTML email body with no encoding — HTML/script injection risk in transactional email [backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs, backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs] — fixed: both fields now pass through `WebUtility.HtmlEncode` before interpolation; added `ShouldHtmlEncodeReasonToPreventInjection` regression test
- [x] [Review][Patch] Currency formatted with `:F2` using the default thread culture instead of a pinned culture — renders inconsistently ("285.00€" vs "285,00 €") depending on server locale [backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs, backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs] — fixed: both handlers now format via `.ToString("C", CultureInfo.GetCultureInfo("fr-FR"))`, which also removes the hardcoded "€" literal
- [x] [Review][Patch] `catch (Exception ex)` also swallows `OperationCanceledException`/`TaskCanceledException` from the `CancellationToken` parameter, logged identically to a genuine send failure — add a dedicated `catch (OperationCanceledException) { throw; }` above the generic catch in all 4 handlers [backend/MonEcommerce/src/Application/Orders/EventHandlers/, backend/MonEcommerce/src/Application/Returns/EventHandlers/] — fixed in all 4 handlers
- [x] [Review][Patch] `OrderShippedEmailHandler`'s failure log omits `TrackingNumber`, inconsistent with the id-logging convention used by the other 3 handlers [backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs] — fixed: log message now includes `{TrackingNumber}`
- [x] [Review][Patch] AC #2 explicitly asks for a comment/TODO referencing the future Story 5.4 integration test for the ≤30s SLA — missing from `OrderShippedEmailHandler.cs` [backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs] — fixed: added TODO comment above the class
- [x] [Review][Defer] No retry/dead-letter path for failed transactional emails [backend/MonEcommerce/src/Application/**/EventHandlers/] — deferred, explicitly in scope of Story 5.4 ("failed email deliveries are retried, max 3 attempts"), not this story
- [x] [Review][Defer] No validation on `CustomerEmail` and no guard on non-positive/zero monetary fields (`TotalInCents`/`AmountInCents`) on the event payloads [backend/MonEcommerce/src/Domain/Events/] — deferred, pre-existing by design: no caller constructs these events yet (pure infrastructure story), so payload validation belongs to the Epic 4/5 stories that will actually raise them from real Order/Refund entities where those invariants are already enforced

## Dev Notes

### This is pure plumbing — nothing publishes these events yet

Epic 4 (checkout/order placement) and Epic 5 (returns/refunds) are still `backlog`. **Do not** modify `Order.cs`, add a `ReturnRequest`/`Refund` entity, or wire any command handler to call `AddDomainEvent(...)`. This story's scope is strictly: the 4 event records + the 4 handlers + their unit tests. The ACs test the handlers directly by constructing the event and calling `.Handle()` — they do not go through a real order-placement flow (that flow doesn't exist yet). Confirmed by grepping the whole `backend/` tree: zero existing references to `OrderPlacedEvent`, `OrderShippedEvent`, `ReturnRequestedEvent`, or `RefundIssuedEvent` before this story.

### The dispatch mechanism already exists — do not touch it

`src/Infrastructure/Data/Interceptors/DispatchDomainEventsInterceptor.cs` already collects `BaseEntity.DomainEvents` on every `SaveChanges`/`SaveChangesAsync` and calls `await _mediator.Publish(domainEvent)` for each one (registered as an EF Core `SaveChangesInterceptor`). This is complete and correct from Story 1.3. Nothing to change here.

`src/Domain/Common/BaseEvent.cs` already exists:
```csharp
public abstract class BaseEvent : INotification
{
}
```
All 4 new events must derive from this (`: BaseEvent`), not directly from `INotification`.

`src/Domain/Common/BaseEntity.cs` already exposes `AddDomainEvent(BaseEvent)` / `DomainEvents` / `ClearDomainEvents()` — this is what future Epic 4/5 stories will call from `Order`/`ReturnRequest`. Not used in this story.

### MediatR registration already auto-discovers notification handlers — no DI changes needed

`src/Application/DependencyInjection.cs`:
```csharp
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
});
```
`RegisterServicesFromAssembly` scans the whole Application assembly and auto-registers every `INotificationHandler<T>` it finds. As long as the 4 handler classes are added anywhere under `src/Application/`, MediatR will pick them up automatically — **do not** add manual `services.AddTransient<INotificationHandler<...>, ...Handler>()` calls, that would be redundant with this project's established convention (confirmed: no existing notification handler is manually registered anywhere in `DependencyInjection.cs`).

Note: the pipeline **Behaviours** (`ValidationBehaviour`, `AuthorizationBehaviour`, etc.) only apply to `IRequest`/`IRequestHandler` (Commands/Queries via `Send`), not to `INotification`/`INotificationHandler` (Events via `Publish`). Domain event handlers do not go through validation/authorization/performance behaviours — this is expected MediatR behavior, not a gap to fix.

### IEmailService — exact signature to call

`src/Application/Common/Interfaces/IEmailService.cs`:
```csharp
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
```
Its SendGrid implementation (`src/Infrastructure/ExternalServices/SendGridEmailService.cs`, done in Story 1.6) already logs SendGrid-level HTTP failures internally and does **not** throw on a non-success SendGrid response — it only throws on things like network exceptions bubbling up from the SendGrid SDK. Handlers in this story must still defensively `try/catch` around the `SendAsync` call per AC #7, since any unhandled exception in an `INotificationHandler` would otherwise propagate back into `DispatchDomainEventsInterceptor.DispatchDomainEvents` and fail the surrounding `SaveChanges` call — exactly what AC #7 forbids.

### Exact fire-and-forget handler pattern to use in all 4 handlers

```csharp
namespace MonEcommerce.Application.Orders.EventHandlers;

public class OrderPlacedEmailHandler : INotificationHandler<OrderPlacedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderPlacedEmailHandler> _logger;

    public OrderPlacedEmailHandler(IEmailService emailService, ILogger<OrderPlacedEmailHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(
                notification.CustomerEmail,
                "Confirmation de votre commande",
                $"Votre commande {notification.OrderId} d'un montant de {notification.TotalInCents / 100m:F2}€ a bien été enregistrée.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order placed email for order {OrderId}", notification.OrderId);
        }
    }
}
```
Replicate this exact shape (constructor injection of `IEmailService` + `ILogger<THandler>`, `try/catch` wrapping `SendAsync`, `LogError` with the exception and a relevant id in the structured log message) for the other 3 handlers. Email subject/body copy can be simple placeholder French text — real HTML templates are out of scope (Story 5.4 "Couverture Complète Emails Transactionnels" owns the final templates).

### Testing conventions — copy the existing pattern exactly

This project uses **NUnit + Moq** (not xUnit, not NSubstitute). Reference file: `tests/Application.UnitTests/Common/Behaviours/RequestLoggerTests.cs`:
```csharp
using Moq;
using NUnit.Framework;

public class RequestLoggerTests
{
    private Mock<ILogger<SampleRequest>> _logger = null!;
    ...
    [SetUp]
    public void Setup() { ... }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated() { ... }
}
```
Follow this exact style: `[SetUp]` method creating `Mock<T>` fields, `[Test]` methods with descriptive `Should...` names, `Times.Once`/`Times.Never` verification. Place new test files under `tests/Application.UnitTests/Orders/EventHandlers/` and `tests/Application.UnitTests/Returns/EventHandlers/` (new folders — mirror the `src/Application/Orders/EventHandlers/` and `src/Application/Returns/EventHandlers/` structure).

To verify a logger call happened (for the failure-swallowed test), use the standard Moq-on-`ILogger` verification pattern:
```csharp
_logger.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

### Project Structure Notes

- New folders created by this story: `src/Domain/Events/`, `src/Application/Orders/EventHandlers/`, `src/Application/Returns/EventHandlers/`, `tests/Application.UnitTests/Orders/EventHandlers/`, `tests/Application.UnitTests/Returns/EventHandlers/`.
- This pre-creates the `Orders`/`Returns` Application feature folders ahead of Epic 4/5 — that's expected and fine; those epics will add `Commands/`/`Queries/` siblings under the same folders later, following the CLAUDE.md "Ajouter une fonctionnalité" pattern.
- No changes needed to `DependencyInjection.cs` (Application or Infrastructure), `Program.cs`, or any existing entity — confirmed via codebase inspection (see Dev Notes above). If you find yourself editing any of those files for this story, stop and re-check the AC — it's out of scope.
- Architecture doc's tree (`_bmad-output/planning-artifacts/architecture.md` line ~465) also places events under `Domain/Events/` with the exact same naming — this story matches that spec exactly.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 1.7: Infrastructure Domain Events] — user story + all acceptance criteria
- [Source: _bmad-output/planning-artifacts/architecture.md#Patterns de Communication] — event/handler naming conventions, `record` immutability requirement
- [Source: _bmad-output/planning-artifacts/architecture.md line ~397-410] — `Domain Events → SendGrid → emails transactionnels ≤ 30s` data flow
- [Source: backend/MonEcommerce/src/Domain/Common/BaseEvent.cs] — existing base class, do not modify
- [Source: backend/MonEcommerce/src/Domain/Common/BaseEntity.cs] — existing `AddDomainEvent`/`DomainEvents` plumbing, not used by this story but informs why event payloads must be self-contained
- [Source: backend/MonEcommerce/src/Infrastructure/Data/Interceptors/DispatchDomainEventsInterceptor.cs] — existing dispatch interceptor, do not modify
- [Source: backend/MonEcommerce/src/Application/DependencyInjection.cs] — existing MediatR registration, confirms auto-discovery of `INotificationHandler<T>`
- [Source: backend/MonEcommerce/src/Application/Common/Interfaces/IEmailService.cs] — exact interface to call
- [Source: backend/MonEcommerce/src/Infrastructure/ExternalServices/SendGridEmailService.cs] — Story 1.6 implementation, informs why handlers still need their own try/catch
- [Source: backend/MonEcommerce/tests/Application.UnitTests/Common/Behaviours/RequestLoggerTests.cs] — exact test framework/style to replicate (NUnit + Moq)
- [Source: _bmad-output/implementation-artifacts/1-4-infrastructure-authentification-jwt.md] — previous story's completion report format/conventions for reference

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `dotnet build MonEcommerce.sln` → Build succeeded, 0 warnings, 0 errors
- `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj` → Passed: 14, Failed: 0 (6 pre-existing + 8 new)
- `Application.FunctionalTests` excluded from `MonEcommerce.sln` (pre-existing, unrelated Aspire `TestAppHost` scaffold gap — not touched by this story); `Domain.UnitTests` and `Infrastructure.IntegrationTests` are empty shells with no test files, confirmed pre-existing

### Completion Notes List

- Implemented exactly as scoped: 4 domain event records + 4 email handlers + 8 unit tests (happy-path + failure-swallowed per handler). No entity, DI, or interceptor changes — confirmed unnecessary per Dev Notes.
- **Deviation from Dev Notes required:** `BaseEvent` was originally a plain `abstract class`. A C# `record` cannot inherit from a non-record class (`CS8864`), so the 4 events (which must be `record` types per AC #5) failed to compile. Fixed by changing `src/Domain/Common/BaseEvent.cs` from `abstract class` to `abstract record` — it has zero members (pure `INotification` marker), so this is a behavior-neutral change; `BaseEntity`'s `List<BaseEvent>` storage and dispatch logic are unaffected.
- Environment note: local machine had .NET SDK 10.0.301 installed but `global.json` pins `9.0.101`. Build/test were verified by temporarily relaxing `global.json` rollForward to `latestMajor`, then reverting it back to the original `9.0.101`/`latestPatch` pin before finishing (confirmed via `git status` — no diff on `global.json`). The .NET 9 SDK itself was not installed on this machine at time of verification.
- **Code review round (2026-07-06):** 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found 1 decision-needed + 6 patch + 2 defer + 2 dismissed findings. Decision (domain-event value-equality on the `BaseEvent` record conversion) resolved as deferred to Epic 4/5 (not reachable by any current code). All 6 patches applied: HTML-encoding for `Reason`/`TrackingNumber`, `fr-FR`-pinned currency formatting (also removes hardcoded "€"), `OperationCanceledException` rethrow guard in all 4 handlers, consistent log fields, AC #2 TODO comment, and tests now assert actual email content instead of `It.IsAny<string>()`. Re-verified after fixes: `dotnet build` 0 errors/0 warnings, `dotnet test` 15/15 passed (was 14; added `ShouldHtmlEncodeReasonToPreventInjection`).

### File List

- `backend/MonEcommerce/src/Domain/Common/BaseEvent.cs` (modified: class → record)
- `backend/MonEcommerce/src/Domain/Events/OrderPlacedEvent.cs` (new)
- `backend/MonEcommerce/src/Domain/Events/OrderShippedEvent.cs` (new)
- `backend/MonEcommerce/src/Domain/Events/ReturnRequestedEvent.cs` (new)
- `backend/MonEcommerce/src/Domain/Events/RefundIssuedEvent.cs` (new)
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderPlacedEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Orders/EventHandlers/OrderShippedEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/ReturnRequestedEmailHandler.cs` (new)
- `backend/MonEcommerce/src/Application/Returns/EventHandlers/RefundIssuedEmailHandler.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/EventHandlers/OrderPlacedEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Orders/EventHandlers/OrderShippedEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/EventHandlers/ReturnRequestedEmailHandlerTests.cs` (new)
- `backend/MonEcommerce/tests/Application.UnitTests/Returns/EventHandlers/RefundIssuedEmailHandlerTests.cs` (new)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified: 1-7 status)
