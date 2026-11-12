using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Row-to-row keys for a list of focusable rows in an items control (#2922): Up and Down move to the previous and next row, Home and End to the first and last,
/// building the row first when the list is virtualized. The keys are always handled, so focus never leaves the list on them. A key with a modifier is left alone,
/// and so is a key typed into a text box in a row. The list takes the keys on the tunnel: by the bubble, Avalonia's directional navigation inside the panel
/// has already moved focus and recycled the row the key came from.
/// </summary>
internal static class RowKeys
{
    /// <summary>True on an items control whose rows are walked with the arrow, Home and End keys.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Enabled", typeof(RowKeys));

    static RowKeys()
    {
        _ = EnabledProperty.Changed.AddClassHandler<ItemsControl>((list, e) =>
        {
            if (e.NewValue is true)
            {
                list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            }
            else
            {
                list.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            }
        });
    }

    /// <summary>Reads <see cref="EnabledProperty"/>.</summary>
    /// <param name="element">The items control.</param>
    /// <returns>True when its rows take the row keys.</returns>
    public static bool GetEnabled(ItemsControl element) => element.GetValue(EnabledProperty);

    /// <summary>Sets <see cref="EnabledProperty"/>.</summary>
    /// <param name="element">The items control.</param>
    /// <param name="value">True to turn the row keys on.</param>
    public static void SetEnabled(ItemsControl element, bool value) => element.SetValue(EnabledProperty, value);

    /// <summary>
    /// Scrolls row <paramref name="index"/> into view, builds it when the list is virtualized, and focuses its focusable row. When the scroll's layout pass has not built it yet,
    /// tries once more at Loaded priority. An index outside the list does nothing.
    /// </summary>
    /// <param name="list">The items control.</param>
    /// <param name="index">The row's index in the list's items.</param>
    internal static void FocusRow(ItemsControl list, int index)
    {
        ArgumentNullException.ThrowIfNull(list);
        FocusRow(list, index, retry: true);
    }

    private static void FocusRow(ItemsControl list, int index, bool retry)
    {
        if (index < 0 || index >= list.ItemCount)
        {
            return;
        }

        list.ScrollIntoView(index);
        list.UpdateLayout();
        var row = list.ContainerFromIndex(index)?.GetSelfAndVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible);
        if (row is not null)
        {
            _ = row.Focus(NavigationMethod.Directional);
        }
        else if (retry)
        {
            Dispatcher.UIThread.Post(() => FocusRow(list, index, retry: false), DispatcherPriority.Loaded);   // the scroll's layout pass builds it
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ItemsControl list || !IsRowKey(e))
        {
            return;
        }

        var current = CurrentIndex(list, e.Source as Visual);
        if (current < 0 || list.ItemCount == 0)
        {
            return;   // the key came from something in the list that is not a row, such as a button in a footer
        }

        var last = list.ItemCount - 1;
        var target = e.Key == Key.Up ? Math.Max(current - 1, 0)
            : e.Key == Key.Down ? Math.Min(current + 1, last)
            : e.Key == Key.Home ? 0
            : last;

        e.Handled = true;
        if (target != current)
        {
            FocusRow(list, target);
        }
    }

    private static bool IsRowKey(KeyEventArgs e) =>
        e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down or Key.Home or Key.End
        && (e.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is null;

    private static int CurrentIndex(ItemsControl list, Visual? source)
    {
        for (var v = source; v is not null && !ReferenceEquals(v, list); v = v.GetVisualParent())
        {
            if (v is Control c && list.IndexFromContainer(c) is var index and >= 0)
            {
                return index;
            }
        }

        return -1;
    }
}
