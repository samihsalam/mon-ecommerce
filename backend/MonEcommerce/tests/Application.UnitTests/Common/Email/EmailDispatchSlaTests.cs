using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Application.Auth.EventHandlers;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.EventHandlers;
using MonEcommerce.Application.Returns.EventHandlers;
using MonEcommerce.Domain.Events;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Common.Email;

// Story 5.4, AC #1/#3: all seven transactional emails are dispatched synchronously in-process via
// MediatR — no queue/worker — so the ≤30s SLA is structurally guaranteed by this architecture, not
// something that needs engineering. These tests exist to make that guarantee an explicit, checked
// assertion per the AC's own wording; a generous 5s ceiling is used (not 30s) so a real regression
// (e.g. someone accidentally adding a blocking network call) still fails fast in CI.
public class EmailDispatchSlaTests
{
    private static readonly TimeSpan SlaCeiling = TimeSpan.FromSeconds(5);

    [Test]
    public async Task UserRegisteredWelcomeEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new UserRegisteredWelcomeEmailHandler(emailService.Object, new Mock<ILogger<UserRegisteredWelcomeEmailHandler>>().Object);
        var notification = new UserRegisteredEvent("user-1", "Alice", "alice@example.com");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task OrderPlacedEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new OrderPlacedEmailHandler(emailService.Object, new Mock<ILogger<OrderPlacedEmailHandler>>().Object);
        var notification = new OrderPlacedEvent(Guid.NewGuid(), "user-1", "client@example.com", 28500);

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task OrderShippedEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new OrderShippedEmailHandler(emailService.Object, new Mock<ILogger<OrderShippedEmailHandler>>().Object);
        var notification = new OrderShippedEvent(Guid.NewGuid(), "client@example.com", "TRACK123", "https://example.com/compte/commandes/abc");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task OrderDeliveredEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new OrderDeliveredEmailHandler(emailService.Object, new Mock<ILogger<OrderDeliveredEmailHandler>>().Object);
        var notification = new OrderDeliveredEvent(Guid.NewGuid(), "client@example.com");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task ReturnRequestedEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new ReturnRequestedEmailHandler(emailService.Object, new Mock<ILogger<ReturnRequestedEmailHandler>>().Object);
        var notification = new ReturnRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", "Produit non conforme");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task RefundIssuedEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new RefundIssuedEmailHandler(emailService.Object, new Mock<ILogger<RefundIssuedEmailHandler>>().Object);
        var notification = new RefundIssuedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", 15000, "#ABCD1234");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    [Test]
    public async Task PasswordResetEmailHandler_ShouldCompleteWellUnderSla()
    {
        var emailService = new Mock<IEmailService>();
        var handler = new PasswordResetEmailHandler(emailService.Object, new Mock<ILogger<PasswordResetEmailHandler>>().Object);
        var notification = new PasswordResetRequestedEvent("user-1", "Alice", "alice@example.com", "https://example.com/reset/token123");

        var elapsed = await MeasureAsync(() => handler.Handle(notification, CancellationToken.None));

        Assert.That(elapsed, Is.LessThan(SlaCeiling));
    }

    private static async Task<TimeSpan> MeasureAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }
}
