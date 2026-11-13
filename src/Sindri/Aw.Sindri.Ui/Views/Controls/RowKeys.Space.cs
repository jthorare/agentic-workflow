using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The keys that work with no cursor row, and the Space tap and hold (#2978).</summary>
internal static partial class RowKeys
{
    // Esc and Space with no cursor row (an empty list, or focus on the list itself or a footer): the page still hears them, with no item, so the peek can close after its last row is gone.
    // j, k and the arrows from the list itself land on the first or last row. A button keeps its own keys.
    // Keys only a RowList (an IRowKeyTarget) answers: Left folds the cursor's group, Right unfolds it; Space, Enter and Esc go to the page.
    private static void OnTargetKey(ItemsControl list, IRowKeyTarget target, int current, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right)
        {
            if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
            {
                e.Handled = target.TryFold(current, collapse: e.Key == Key.Left);
            }
        }
        else if (e.Key == Key.OemPeriod && (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null)
        {
            Raise(list, ActionsRequestedEvent, current);   // the tunnel handler runs before the shell's key handler, which never sees a handled key
            e.Handled = true;
        }
        else if (e.Key is Key.Space or Key.Enter or Key.Escape)
        {
            OnListKey(list, current, e);
        }
    }

    private static void OffRowKey(ItemsControl list, KeyEventArgs e)
    {
        if (list is not IRowKeyTarget || (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (e.Key is Key.Escape or Key.Space)
        {
            e.Handled = e.Key == Key.Space || !SheetOpen(list);
            if (e.Key == Key.Space)
            {
                SpaceDown(list, -1);
            }
            else if (e.Handled)
            {
                Raise(list, BackRequestedEvent, -1);
            }
        }
        else if (ReferenceEquals(e.Source, list) && IsMoveKey(e.Key, list))
        {
            JumpFromList(list, e);
        }
    }

    // A move key typed on the list itself lands on its first row (down, Home) or its last (up, End).
    private static void JumpFromList(ItemsControl list, KeyEventArgs e)
    {
        var down = e.Key is Key.Down or Key.J or Key.Home;
        FocusRow(list, FirstRow(list, down ? 0 : list.ItemCount - 1, down ? 1 : -1, -1));
        e.Handled = list.ItemCount > 0;
    }

    // The first Space down starts the clock; a repeat after the hold time shows the peek, and the key up ends it. A repeat inside the tap time is not a second tap.
    private static void SpaceDown(ItemsControl list, int current)
    {
        var state = _space.GetOrCreateValue(list);
        if (state.Start is not { } start)
        {
            state.Start = Clock.GetTimestamp();
            state.Index = current;
            state.HoldRaised = false;
        }
        else if (!state.HoldRaised && Clock.GetElapsedTime(start) >= _holdAfter)
        {
            state.HoldRaised = true;
            Raise(list, PeekRequestedEvent, state.Index, hold: true, released: false);
        }
    }

    // A Space down whose key up goes elsewhere must not turn the next tap into a hold: focus leaving the list ends the press.
    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is ItemsControl list && _space.TryGetValue(list, out var state) && state.Start is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!list.IsKeyboardFocusWithin)
                {
                    state.Start = null;
                }
            });
        }
    }

    private static void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is not ItemsControl list || e.Key != Key.Space || !_space.TryGetValue(list, out var state) || state.Start is not { } start)
        {
            return;
        }

        if (!IsPlain(e))
        {
            state.Start = null;   // a modifier or a text box: no peek from this key up, and the press ends so the next Space is a fresh tap
            return;
        }

        var held = state.HoldRaised || Clock.GetElapsedTime(start) >= _holdAfter;
        if (held && !state.HoldRaised)
        {
            Raise(list, PeekRequestedEvent, state.Index, hold: true, released: false);
        }

        Raise(list, PeekRequestedEvent, state.Index, hold: held, released: held);
        state.Start = null;
        e.Handled = true;
    }
}
