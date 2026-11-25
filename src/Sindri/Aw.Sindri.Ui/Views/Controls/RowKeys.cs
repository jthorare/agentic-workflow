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
/// has already moved focus and recycled the row the key came from. A list that is an <see cref="IRowKeyTarget"/> (the RowList, #2978) also takes j and k (Down and Up),
/// Space (a tap toggles the peek, a hold shows it until the key comes up), Enter (open) and Esc (back, unless a sheet is open), and skips the entries that are not rows.
/// </summary>
internal static partial class RowKeys
{
    private static readonly TimeSpan _holdAfter = TimeSpan.FromMilliseconds(250);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ItemsControl, SpaceState> _space = [];

    /// <summary>True on an items control whose rows are walked with the arrow, Home and End keys.</summary>
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<ItemsControl, bool>("Enabled", typeof(RowKeys));

    /// <summary>Raised on the list when Space asks to peek the cursor row: a tap toggles the peek, a hold shows it until the key comes up.</summary>
    public static readonly RoutedEvent<RowKeyEventArgs> PeekRequestedEvent = RoutedEvent.Register<ItemsControl, RowKeyEventArgs>("PeekRequested", RoutingStrategies.Bubble);

    /// <summary>Raised on the list when Enter asks to open the cursor row in full.</summary>
    public static readonly RoutedEvent<RowKeyEventArgs> OpenRequestedEvent = RoutedEvent.Register<ItemsControl, RowKeyEventArgs>("OpenRequested", RoutingStrategies.Bubble);

    /// <summary>Raised on the list when `.` asks for the cursor row's actions. RowKeys handles the key before the shell's own handler sees it.</summary>
    public static readonly RoutedEvent<RowKeyEventArgs> ActionsRequestedEvent = RoutedEvent.Register<ItemsControl, RowKeyEventArgs>("ActionsRequested", RoutingStrategies.Bubble);

    /// <summary>Raised on the list when Esc asks to step back, unless a sheet is open.</summary>
    public static readonly RoutedEvent<RowKeyEventArgs> BackRequestedEvent = RoutedEvent.Register<ItemsControl, RowKeyEventArgs>("BackRequested", RoutingStrategies.Bubble);

    static RowKeys()
    {
        _ = EnabledProperty.Changed.AddClassHandler<ItemsControl>((list, e) =>
        {
            if (e.NewValue is true)
            {
                list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
                list.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
                list.AddHandler(InputElement.LostFocusEvent, OnLostFocus);
            }
            else
            {
                list.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
                list.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
                list.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);
            }
        });
    }

    /// <summary>The clock that tells a Space tap (key up under 250 ms) from a hold. Tests set a fake one.</summary>
    internal static TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>Test seam: when set, answers "is a sheet open" in place of the list's window. Tests set it and reset it to null.</summary>
    internal static Func<bool>? IsSheetOpen { get; set; }

    // True while a sheet is open: Esc then belongs to the sheet and the list does not raise BackRequestedEvent. Read from the shell of the list's own window, so no window state outlives it.
    private static bool SheetOpen(ItemsControl list) =>
        IsSheetOpen?.Invoke() ?? TopLevel.GetTopLevel(list)?.DataContext is ViewModels.Shell.ShellViewModel { IsSheetOpen: true };

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

        if (list is IRowKeyTarget target)
        {
            target.RevealRow(index);
        }
        else
        {
            list.ScrollIntoView(index);
            list.UpdateLayout();
        }

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
        if (sender is not ItemsControl list || !IsPlain(e))
        {
            return;
        }

        var current = CurrentIndex(list, e.Source as Visual);
        if (current < 0 || list.ItemCount == 0)
        {
            OffRowKey(list, e);   // the key came from the list itself, or from something in it that is not a row, such as a button in a footer
            return;
        }

        if (IsMoveKey(e.Key, list))
        {
            e.Handled = true;
            var target = TargetIndex(list, current, e.Key);
            if (target != current)
            {
                FocusRow(list, target);
            }
        }
        else if (list is IRowKeyTarget target)
        {
            OnTargetKey(list, target, current, e);
        }
    }

    // No modifier, and not typed into a text box.
    private static bool IsPlain(KeyEventArgs e) => e.KeyModifiers == KeyModifiers.None && (e.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is null;

    private static bool IsMoveKey(Key key, ItemsControl list) => key is Key.Up or Key.Down or Key.Home or Key.End || (list is IRowKeyTarget && key is Key.J or Key.K);

    // The target row of a move key. A list with entries that are not rows (group headers) steps over them.
    private static int TargetIndex(ItemsControl list, int current, Key key)
    {
        var step = key is Key.Down or Key.J ? 1 : -1;
        if (key is Key.Up or Key.K or Key.Down or Key.J)
        {
            return FirstRow(list, current + step, step, current);
        }

        return key == Key.Home ? FirstRow(list, 0, 1, current) : FirstRow(list, list.ItemCount - 1, -1, current);
    }

    private static int FirstRow(ItemsControl list, int from, int step, int otherwise)
    {
        for (var i = from; i >= 0 && i < list.ItemCount; i += step)
        {
            if (IsRow(list, i))
            {
                return i;
            }
        }

        return otherwise;
    }

    private static bool IsRow(ItemsControl list, int index) => list is not IRowKeyTarget target || target.IsRowIndex(index);

    // Space, Enter and Esc on a RowList. A button in a row keeps its own Space and Enter.
    private static void OnListKey(ItemsControl list, int current, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter && (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (e.Key == Key.Space)
        {
            SpaceDown(list, current);
        }
        else if (e.Key == Key.Enter)
        {
            Raise(list, OpenRequestedEvent, current);
        }
        else if (SheetOpen(list))
        {
            return;   // Esc belongs to the sheet
        }
        else
        {
            Raise(list, BackRequestedEvent, current);
        }

        e.Handled = true;
    }

    private static void Raise(ItemsControl list, RoutedEvent<RowKeyEventArgs> routed, int index, bool hold = false, bool released = false) =>
        list.RaiseEvent(new RowKeyEventArgs { RoutedEvent = routed, Source = list, Index = index, Hold = hold, Released = released });

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

    // Where a Space press stands on one list.
    private sealed class SpaceState
    {
        internal long? Start { get; set; }

        internal int Index { get; set; }

        internal bool HoldRaised { get; set; }
    }
}
