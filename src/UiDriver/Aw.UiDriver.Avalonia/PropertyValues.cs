using System.Globalization;
using Avalonia;

namespace Aw.UiDriver.Avalonia;

/// <summary>An element's registered Avalonia properties as text, read through the property registry (no reflection).</summary>
internal static class PropertyValues
{
    /// <summary>The properties the element has a value for, by name.</summary>
    /// <param name="element">The element.</param>
    /// <returns>Name to the value as text; a property that can't be read or has no value is left out.</returns>
    internal static IReadOnlyDictionary<string, string> Of(AvaloniaObject element)
    {
        SortedDictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(element))
        {
            try
            {
                var value = element.GetValue(property);
                if (value is not null && !ReferenceEquals(value, AvaloniaProperty.UnsetValue))
                {
                    values[property.Name] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch (Exception e) when (e is InvalidOperationException or InvalidCastException or ArgumentException)
            {
                // A property whose value can't be read is left out; the answer is what could be read.
            }
        }

        return values;
    }
}
