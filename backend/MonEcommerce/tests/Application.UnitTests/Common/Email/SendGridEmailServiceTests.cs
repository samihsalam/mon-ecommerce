using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.ExternalServices;
using NUnit.Framework;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MonEcommerce.Application.UnitTests.Common.Email;

// Story 5.4, Task 8: none of this retry/logging logic existed before this story, so there was no
// prior test to extend — this is a from-scratch suite for SendGridEmailService itself (as opposed
// to the handlers that call it, covered by each handler's own *EmailHandlerTests.cs).
public class SendGridEmailServiceTests
{
    private Mock<ISendGridClient> _client = null!;
    private IDbContextFactory<ApplicationDbContext> _dbContextFactory = null!;
    private IConfiguration _configuration = null!;
    private SendGridEmailService _service = null!;

    [SetUp]
    public void Setup()
    {
        _client = new Mock<ISendGridClient>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new FakeDbContextFactory(options);

        var configValues = new Dictionary<string, string?>
        {
            ["SendGrid:FromEmail"] = "noreply@monecommerce.test",
            ["SendGrid:FromName"] = "MonEcommerce",
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        _service = new SendGridEmailService(_client.Object, _dbContextFactory, _configuration, new Mock<ILogger<SendGridEmailService>>().Object);
    }

    [Test]
    public async Task ShouldSucceedOnFirstAttemptAndLogOneSuccessRowWithMessageId()
    {
        _client
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(HttpStatusCode.Accepted, messageId: "msg-success-1"));

        await _service.SendAsync("client@example.com", "Sujet", "<p>Corps</p>", "OrderPlaced", CancellationToken.None);

        _client.Verify(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Once);

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var logs = context.EmailDispatchLogs.ToList();

        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].Success, Is.True);
        Assert.That(logs[0].AttemptCount, Is.EqualTo(1));
        Assert.That(logs[0].SendGridMessageId, Is.EqualTo("msg-success-1"));
        Assert.That(logs[0].EventType, Is.EqualTo("OrderPlaced"));
        Assert.That(logs[0].Recipient, Is.EqualTo("client@example.com"));
        // Set explicitly by LogDispatchAsync, not by an interceptor — the independent factory
        // context configures none. Regression check for a bug caught only by actually booting the
        // app (dotnet build/test never exercise the real DI container).
        Assert.That(logs[0].Created, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Test]
    public async Task ShouldRetryAndSucceedOnSecondAttempt()
    {
        _client
            .SetupSequence(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(HttpStatusCode.InternalServerError))
            .ReturnsAsync(CreateResponse(HttpStatusCode.Accepted, messageId: "msg-success-2"));

        await _service.SendAsync("client@example.com", "Sujet", "<p>Corps</p>", "OrderShipped", CancellationToken.None);

        _client.Verify(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var logs = context.EmailDispatchLogs.ToList();

        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].Success, Is.True);
        Assert.That(logs[0].AttemptCount, Is.EqualTo(2));
        Assert.That(logs[0].SendGridMessageId, Is.EqualTo("msg-success-2"));
    }

    [Test]
    public async Task ShouldRetryAndSucceedOnThirdAttempt()
    {
        _client
            .SetupSequence(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(HttpStatusCode.InternalServerError))
            .ReturnsAsync(CreateResponse(HttpStatusCode.InternalServerError))
            .ReturnsAsync(CreateResponse(HttpStatusCode.Accepted, messageId: "msg-success-3"));

        await _service.SendAsync("client@example.com", "Sujet", "<p>Corps</p>", "OrderDelivered", CancellationToken.None);

        _client.Verify(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var logs = context.EmailDispatchLogs.ToList();

        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].Success, Is.True);
        Assert.That(logs[0].AttemptCount, Is.EqualTo(3));
    }

    [Test]
    public void ShouldExhaustAllAttemptsThrowAndLogOneFailureRow()
    {
        _client
            .Setup(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(HttpStatusCode.InternalServerError, body: "SendGrid is down"));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SendAsync("client@example.com", "Sujet", "<p>Corps</p>", "RefundIssued", CancellationToken.None));

        _client.Verify(c => c.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        using var context = _dbContextFactory.CreateDbContext();
        var logs = context.EmailDispatchLogs.ToList();

        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs[0].Success, Is.False);
        Assert.That(logs[0].AttemptCount, Is.EqualTo(3));
        Assert.That(logs[0].SendGridMessageId, Is.Null);
        Assert.That(logs[0].ErrorMessage, Does.Contain("SendGrid is down"));
    }

    private static Response CreateResponse(HttpStatusCode statusCode, string? messageId = null, string body = "")
    {
        var responseMessage = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };

        if (messageId is not null)
        {
            responseMessage.Headers.Add("X-Message-Id", messageId);
        }

        return new Response(responseMessage.StatusCode, responseMessage.Content, responseMessage.Headers);
    }

    // Minimal stand-in for Microsoft.EntityFrameworkCore's pooled factory — all this test needs is
    // "each CreateDbContext(Async) call returns a new context instance bound to the same
    // in-memory database name", so writes from one instance are visible when queried from another.
    private sealed class FakeDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }
}
