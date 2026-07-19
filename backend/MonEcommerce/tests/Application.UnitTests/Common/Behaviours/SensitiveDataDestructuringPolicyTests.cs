using MonEcommerce.Infrastructure.Logging;
using NUnit.Framework;
using Serilog.Core;
using Serilog.Events;

namespace MonEcommerce.Application.UnitTests.Common.Behaviours;

public class SensitiveDataDestructuringPolicyTests
{
    private sealed record SampleRequestWithPassword(string Email, string Password);

    private sealed class PassthroughPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
            => new ScalarValue(value);
    }

    [Test]
    public void ShouldRedactPasswordProperty()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var request = new SampleRequestWithPassword("user@example.com", "super-secret-password");

        var handled = policy.TryDestructure(request, new PassthroughPropertyValueFactory(), out var result);

        Assert.That(handled, Is.True);
        var structure = (StructureValue)result!;
        var passwordProperty = structure.Properties.Single(p => p.Name == "Password");
        var redacted = (ScalarValue)passwordProperty.Value;

        Assert.That(redacted.Value, Is.EqualTo("***REDACTED***"));
        Assert.That(structure.Properties.Any(p => p.Name == "Email"), Is.True);
    }

    [Test]
    public void ShouldNotHandleTypesWithoutAPasswordProperty()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var request = new { Email = "user@example.com" };

        var handled = policy.TryDestructure(request, new PassthroughPropertyValueFactory(), out var result);

        Assert.That(handled, Is.False);
        Assert.That(result, Is.Null);
    }
}
