using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Arrow keys on a result list (#3677, decision 4): with ResultListKeys.Arrows on an ItemsControl, Down and Up move the focus between its rows (the buttons with the "result-open" class)
/// and stop at the ends; Enter on a row presses it, which the button already does.
/// </summary>
#pragma warning disable MA0182 // the lists use it from XAML; the tests use it now
internal static class ResultListKeys
{
    /// <summary>Whether Down and Up move between the rows of the list.</summary>
    public static readonly AttachedProperty<bool> ArrowsProperty = AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Arrows", typeof(ResultListKeys));

    static ResultListKeys()
    {
        _ = ArrowsProperty.Changed.AddClassHandler<ItemsControl>((list, e) =>
        {
            list.RemoveHandler(InputElement.KeyDownEvent, OnKey);
            if (e.NewValue is true)
            {
                list.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Bubble);
            }
        });
    }

    /// <summary>Reads the property.</summary>
    /// <param name="list">The list.</param>
    /// <returns>Whether the arrows work on it.</returns>
    public static bool GetArrows(ItemsControl list) => list?.GetValue(ArrowsProperty) ?? false;

    /// <summary>Sets the property.</summary>
    /// <param name="list">The list.</param>
    /// <param name="value">Whether the arrows work on it.</param>
    public static void SetArrows(ItemsControl list, bool value) => list?.SetValue(ArrowsProperty, value);

    private static void OnKey(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && (e.Key is Key.Down or Key.Up) && sender is ItemsControl list && e.Source is Control source && Move(list, source, e.Key == Key.Down ? 1 : -1))
        {
            e.Handled = true;
        }
    }

    private static bool Move(ItemsControl list, Control source, int step)
    {
        var rows = list.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("result-open") && b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        var at = rows.FindIndex(r => ReferenceEquals(r, source) || r.IsVisualAncestorOf(source));
        if (at < 0)
        {
            return false;
        }

        _ = rows[Math.Clamp(at + step, 0, rows.Count - 1)].Focus(NavigationMethod.Directional);
        return true;
    }
}
#pragma warning restore MA0182
