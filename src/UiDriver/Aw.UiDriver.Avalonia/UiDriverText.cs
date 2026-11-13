using Avalonia;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The text a control draws itself, for the driver (#3189). A control that paints its text instead of holding TextBlocks sets this, and a selector's text and textContains
/// find it. The driver reads it first, then the text of the usual text controls.
/// </summary>
public static class UiDriverText
{
    /// <summary>The text the control draws.</summary>
    public static readonly AttachedProperty<string?> TextProperty = AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Text", typeof(UiDriverText));

    /// <summary>Reads <see cref="TextProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <returns>The text it draws, or null.</returns>
    public static string? GetText(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(TextProperty);
    }

    /// <summary>Sets <see cref="TextProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <param name="value">The text it draws.</param>
    public static void SetText(AvaloniaObject element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(TextProperty, value);
    }
}
