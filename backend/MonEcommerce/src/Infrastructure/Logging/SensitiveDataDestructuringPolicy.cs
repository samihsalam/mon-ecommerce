using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace MonEcommerce.Infrastructure.Logging;

/// <summary>
/// Redacts sensitive properties (case-insensitive) before Serilog's <c>{@Request}</c>
/// destructuring (used by <c>LoggingBehaviour&lt;TRequest&gt;</c>) would otherwise log them in
/// plain text. Applies to any logged object, not just Auth commands.
/// </summary>
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "NewPassword",
        "CurrentPassword",
        "Token",
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (!properties.Any(p => SensitivePropertyNames.Contains(p.Name)))
        {
            result = null!;
            return false;
        }

        var structureProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            if (SensitivePropertyNames.Contains(prop.Name))
            {
                structureProperties.Add(new LogEventProperty(prop.Name, new ScalarValue("***REDACTED***")));
                continue;
            }

            object? propValue;
            try
            {
                propValue = prop.GetValue(value);
            }
            catch
            {
                continue;
            }

            structureProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true)));
        }

        result = new StructureValue(structureProperties, type.Name);
        return true;
    }
}
