using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Arrow keys on a result list (#3677, decision 4): with ResultListKeys.Arrows on an ItemsControl, Down and Up move the focus between its rows (the buttons with the "result-open" class), and Enter on a row presses it.
/// Not built yet.
/// </summary>
#pragma warning disable MA0182 // the lists use it from XAML; the tests use it now
internal static class ResultListKeys
{
    /// <summary>Whether Down and Up move between the rows of the list.</summary>
    public static readonly AttachedProperty<bool> ArrowsProperty = AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Arrows", typeof(ResultListKeys));

    /// <summary>Reads the property.</summary>
    /// <param name="list">The list.</param>
    /// <returns>Whether the arrows work on it.</returns>
    public static bool GetArrows(ItemsControl list) => list?.GetValue(ArrowsProperty) ?? false;

    /// <summary>Sets the property.</summary>
    /// <param name="list">The list.</param>
    /// <param name="value">Whether the arrows work on it.</param>
    public static void SetArrows(ItemsControl list, bool value) => list?.SetValue(ArrowsProperty, value);
}
#pragma warning restore MA0182
