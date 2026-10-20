using Avalonia;
using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

// The pointer half of the stage: a drag on empty canvas pans, the wheel pans, Ctrl or Cmd with the wheel zooms.
internal sealed partial class ZoomBorder
{
    private (Point Pointer, ZoomView View, bool Moved)? _drag;

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Settle();
        _drag = (e.GetPosition(this), View, false);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } drag)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _drag = null;   // the button went up where this control could not see it
            return;
        }

        var delta = e.GetPosition(this) - drag.Pointer;
        _drag = drag with { Moved = drag.Moved || Math.Abs(delta.X) + Math.Abs(delta.Y) > ClickSlop };
        Apply(Kept(drag.View.PanBy(delta.X, delta.Y)));
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is { } drag)
        {
            _drag = null;
            e.Pointer.Capture(control: null);
            if (!drag.Moved)
            {
                EmptyClicked?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = null;   // a window that lost focus, a popup or a cancelled touch took the pointer
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Settle();
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != KeyModifiers.None)
        {
            Apply(Kept(View.ZoomAt(e.GetPosition(this), Math.Pow(1.1, e.Delta.Y), Range)));
        }
        else
        {
            Apply(Kept(View.PanBy(-e.Delta.X * WheelStep, e.Delta.Y * WheelStep)));
        }

        e.Handled = true;
    }
}

#pragma warning restore MA0182
