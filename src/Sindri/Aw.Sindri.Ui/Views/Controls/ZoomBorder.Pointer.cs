using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

// The pointer half of the stage: a drag on empty canvas pans, the wheel pans, Ctrl or Cmd with the wheel zooms.
internal sealed partial class ZoomBorder
{
    private readonly PinchTracker _pinch = new();
    private (Point Pointer, ZoomView View, bool Moved)? _drag;

    // A touch pinch (the recognizer) and a trackpad pinch (macOS magnify) both zoom around their origin.
    private void WirePinch()
    {
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(PinchEvent, OnPinch);
        AddHandler(PinchEndedEvent, (_, _) => _pinch.End());
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnMagnify);
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        ZoomBy(e.ScaleOrigin, _pinch.Step(e.Scale));
        e.Handled = true;
    }

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        ZoomBy(e.GetPosition(this), PinchTracker.MagnifyFactor(e.Delta.X));
        e.Handled = true;
    }

    private void ZoomBy(Point origin, double factor)
    {
        Settle();
        Apply(Kept(View.ZoomAt(origin, factor, Range)));
    }

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
        PseudoClasses.Set(":panning", value: true);
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
            PseudoClasses.Set(":panning", value: false);
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
            PseudoClasses.Set(":panning", value: false);
            e.Pointer.Capture(control: null);
            if (!drag.Moved)
            {
                EmptyClicked?.Invoke(this, EventArgs.Empty);
                Clicked?.Invoke(this, e);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = null;   // a window that lost focus, a popup or a cancelled touch took the pointer
        PseudoClasses.Set(":panning", value: false);
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
