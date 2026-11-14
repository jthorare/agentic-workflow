using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.UiDriver.Core;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// Hover as a real pointer: a move to the element's centre goes in through the window's own input, the way the platform reports a mouse, so
/// <see cref="InputElement.IsPointerOver"/> flips and the element's pointer handlers run (#3467). These input types are Avalonia's unstable API
/// (see the project file).
/// </summary>
internal static partial class ElementActions
{
    // One driver mouse for every window; the input manager keys pointer-over state by device.
    private static readonly MouseDevice _mouse = new();

    /// <summary>
    /// Moves the driver's pointer onto the element's centre, or out of the window. The pointer stays over the element until `off` or the window
    /// closes (one driver mouse). When no real move reaches the element (it is in no window, or something covers it), it sets or clears the
    /// :pointerover pseudo-class instead, which styles follow.
    /// </summary>
    /// <param name="visual">The element.</param>
    /// <param name="on">True to move onto it, false to move out.</param>
    /// <returns>Success, or AW-UIDRIVER-003 for an element that has no classes.</returns>
    internal static Result<Unit> Hover(Visual visual, bool on)
    {
        if (visual is not StyledElement element)
        {
            return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} has no style classes to hover");
        }

        var moved = Move(visual, on);
        if (!on || !moved || visual is not InputElement { IsPointerOver: true })
        {
            ((IPseudoClasses)element.Classes).Set(":pointerover", on);
        }

        return Unit.Value;
    }

    // Sends one raw pointer event: a move to the centre (on), or the pointer leaving the window (off). False when it couldn't be sent.
    private static bool Move(Visual visual, bool on)
    {
        if (TopLevel.GetTopLevel(visual) is not { PlatformImpl.Input: { } input } top || visual.GetPresentationSource() is not IInputRoot root)
        {
            return false;
        }

        var centre = on ? visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), top) : new Point(-1, -1);
        if (centre is not { } at)
        {
            return false;
        }

        var type = on ? RawPointerEventType.Move : RawPointerEventType.LeaveWindow;
        input(new RawPointerEventArgs(_mouse, (ulong)Environment.TickCount64, root, type, at, RawInputModifiers.None));
        return true;
    }
}
