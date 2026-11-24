using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The canvas's pointer, wheel and keyboard input (<see cref="GraphCanvas"/>, continued): drag to pan, the wheel, Ctrl+wheel zoom, the selection keys
/// and F for Fit.</summary>
internal sealed partial class GraphCanvas
{
    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus();
        var at = e.GetPosition(this);
        if (NodeAt(at) is { } id)
        {
            _ = Graph?.SelectNode(id);
        }
        else
        {
            _fitPending = false;
            _panFrom = at;
            _panStart = Pan;
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var at = e.GetPosition(this);
        if (_panFrom is { } from)
        {
            PanTo(new Point(_panStart.X + (at.X - from.X), _panStart.Y + (at.Y - from.Y)));
            return;
        }

        ToolTip.SetTip(this, TipAt(at));   // the card's full settings line, which the card may cut (#4203); none off the cards
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panFrom is not null)
        {
            _panFrom = null;
            e.Pointer.Capture(control: null);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        e.Handled = Wheel(e.Delta, e.KeyModifiers, e.GetPosition(this));
    }

    /// <summary>One wheel step. Ctrl+wheel zooms about the pointer. A plain wheel scrolls up and down; a sideways swipe (or Shift+wheel) scrolls left and right. The canvas takes the
    /// wheel only while it can move that way; at its end, or when it fits, the wheel is left to the page.</summary>
    /// <param name="delta">The wheel's delta.</param>
    /// <param name="modifiers">The keys held.</param>
    /// <param name="at">The pointer, in this control.</param>
    /// <returns>True when the canvas took the wheel.</returns>
    internal bool Wheel(Vector delta, KeyModifiers modifiers, Point at)
    {
        _fitPending = false;
        if (!modifiers.HasFlag(KeyModifiers.Control))
        {
            var (dx, dy) = modifiers.HasFlag(KeyModifiers.Shift) && Math.Abs(delta.X) < 1e-9 ? (delta.Y, 0d) : (delta.X, delta.Y);
            var before = Pan;
            PanTo(new Point(Pan.X + (dx * WheelStep), Pan.Y + (dy * WheelStep)));
            return Pan != before;
        }

        // Zoom about the pointer: the world point under it stays under it.
        var world = ToWorld(at);
        Zoom *= Math.Pow(ZoomStep, delta.Y);
        PanTo(new Point(at.X - (world.X * Zoom), at.Y - (world.Y * Zoom)));
        return true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Graph is not { } graph)
        {
            return;
        }

        if (ZoomKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;   // a zoom key zooms about the centre and leaves the selection where it is
            return;
        }

#pragma warning disable IDE0072 // every other key is not the canvas's
        e.Handled = e.Key switch
        {
            Key.F when e.KeyModifiers == KeyModifiers.None => FitKey(),
            Key.Right or Key.Down => graph.SelectNext(),
            Key.Left or Key.Up => graph.SelectPrevious(),
            Key.Tab => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? graph.SelectPrevious() : graph.SelectNext(),   // at either end Tab is not handled, so focus moves on
            Key.Enter => graph.ActivateSelected(),
            Key.Delete => graph.RequestRemoveSelected(),   // a request only: the edit bar asks before anything is sent
            _ => false,
        };
#pragma warning restore IDE0072
        if (e.Handled && graph.SelectedId is { } id)
        {
            ScrollIntoView(id);
        }
    }

    private bool FitKey()
    {
        FitNow();
        return true;
    }
}
