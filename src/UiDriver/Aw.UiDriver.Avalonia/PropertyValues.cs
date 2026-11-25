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
                    values[property.Name] = Text(value);
                }
            }
            catch (Exception e) when (e is InvalidOperationException or InvalidCastException or ArgumentException)
            {
                // A property whose value can't be read is left out; the answer is what could be read.
            }
        }

        return values;
    }

    /// <summary>The most items a collection value prints; an `ItemsSource` of thousands of rows prints these and the count left out.</summary>
    internal const int MaxItems = 20;

    /// <summary>A value as text. A collection (a string is not one here) is its items' text joined with ", ": most collections have no
    /// ToString of their own, so `FontFeatures` would print as `Avalonia.Media.FontFeatureCollection` (#4270). Past
    /// <see cref="MaxItems"/> items the text ends with ", +N more".</summary>
    private static string Text(object value)
    {
        if (value is string || value is not System.Collections.IEnumerable items)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        List<string> shown = [];
        var more = 0;
        foreach (var item in items)
        {
            if (shown.Count < MaxItems)
            {
                shown.Add(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);
            }
            else
            {
                more++;
            }
        }

        return more == 0 ? string.Join(", ", shown) : string.Join(", ", shown) + ", +" + more.ToString(CultureInfo.InvariantCulture) + " more";
    }
}
