using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Selectors;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// What the driver does to an element, on the UI thread. Clicks and actions go through the element's automation peer (the providers a screen
/// reader uses: invoke, toggle, select, expand), so they run the same commands a person's click does; typing sets a text box; keys are raised as
/// key events on the element. Nothing reads a property by name.
/// </summary>
internal static class ElementActions
{
    private static readonly string[] _clickActions = ["invoke", "toggle", "select", "expand"];
    private static readonly string[] _providerActions = ["INVOKE", "TOGGLE", "SELECT", "EXPAND", "COLLAPSE"];

    /// <summary>Clicks: invoke, else toggle, else select, else expand or collapse.</summary>
    /// <param name="visual">The element.</param>
    /// <returns>Success, or AW-UIDRIVER-003 when the element is disabled or has no way to be clicked.</returns>
    internal static Result<Unit> Click(Visual visual)
    {
        foreach (var action in _clickActions)
        {
            var done = Do(visual, action);
            if (done.IsSuccess)
            {
                return done;
            }
        }

        return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} has nothing to click (no invoke, toggle, select or expand)");
    }

    /// <summary>Runs a named action: invoke, toggle, select, expand, collapse or focus.</summary>
    /// <param name="visual">The element.</param>
    /// <param name="action">The action's name.</param>
    /// <returns>Success, AW-UIDRIVER-001 for an unknown name, or AW-UIDRIVER-003 when the element can't do it.</returns>
    internal static Result<Unit> Do(Visual visual, string action)
    {
        if (visual is InputElement { IsEffectivelyEnabled: false })
        {
            return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} is disabled");
        }

        if (action.Equals("focus", StringComparison.OrdinalIgnoreCase))
        {
            return visual is InputElement input && input.Focus() ? Unit.Value : Missing(visual, action);
        }

        var peer = visual is Control control ? ControlAutomationPeer.CreatePeerForElement(control) : null;
        var run = peer is null ? null : Provided(peer, action.ToUpperInvariant());
        return run is not null
            ? Run(run)
            : _providerActions.Contains(action.ToUpperInvariant(), StringComparer.Ordinal)
                ? Missing(visual, action)
                : UiDriverErrors.Invalid.With($"'{action}' is not an action (invoke, toggle, select, expand, collapse, focus)");
    }

    // The provider call for an action the peer supports, or null.
    private static Action? Provided(AutomationPeer peer, string action) => action switch
    {
        "INVOKE" => peer.GetProvider<IInvokeProvider>() is { } invoke ? new Action(invoke.Invoke) : null,
        "TOGGLE" => peer.GetProvider<IToggleProvider>() is { } toggle ? new Action(toggle.Toggle) : null,
        "SELECT" => peer.GetProvider<ISelectionItemProvider>() is { } item ? new Action(item.Select) : null,
        "EXPAND" => peer.GetProvider<IExpandCollapseProvider>() is { } expand ? new Action(expand.Expand) : null,
        "COLLAPSE" => peer.GetProvider<IExpandCollapseProvider>() is { } collapse ? new Action(collapse.Collapse) : null,
        _ => null,
    };

    /// <summary>Types into a text box.</summary>
    /// <param name="visual">The element; it must be a text box.</param>
    /// <param name="text">The text.</param>
    /// <param name="replace">Whether to replace what the box holds.</param>
    /// <returns>Success, or AW-UIDRIVER-003.</returns>
    internal static Result<Unit> Type(Visual visual, string text, bool replace)
    {
        if (visual is not TextBox box)
        {
            return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} is not a text box");
        }

        if (!box.IsEffectivelyEnabled || box.IsReadOnly)
        {
            return UiDriverErrors.NotActionable.With("the text box is disabled or read-only");
        }

        _ = box.Focus();
        box.Text = replace ? text : (box.Text ?? string.Empty) + text;
        box.CaretIndex = box.Text.Length;
        return Unit.Value;
    }

    /// <summary>Raises a key press (down, then up) on an element.</summary>
    /// <param name="visual">The element.</param>
    /// <param name="chord">The chord.</param>
    /// <returns>Success, or AW-UIDRIVER-001 for a key that is none.</returns>
    internal static Result<Unit> Press(Visual visual, KeyChord chord)
    {
        if (!Enum.TryParse<Key>(chord.Key, ignoreCase: true, out var key) || !Enum.IsDefined(key))
        {
            return UiDriverErrors.Invalid.With($"'{chord.Key}' is not a key name");
        }

        if (visual is not InputElement target)
        {
            return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} takes no keys");
        }

        var modifiers = (chord.Control ? KeyModifiers.Control : KeyModifiers.None) | (chord.Shift ? KeyModifiers.Shift : KeyModifiers.None)
            | (chord.Alt ? KeyModifiers.Alt : KeyModifiers.None) | (chord.Meta ? KeyModifiers.Meta : KeyModifiers.None);
        foreach (var routed in new[] { InputElement.KeyDownEvent, InputElement.KeyUpEvent })
        {
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = routed, Source = target, Key = key, KeyModifiers = modifiers });
        }

        return Unit.Value;
    }

    /// <summary>Scrolls by an offset.</summary>
    /// <param name="visual">The element: a scroll viewer, one inside a scroll viewer, or one that holds a scroll viewer.</param>
    /// <param name="dx">Pixels right.</param>
    /// <param name="dy">Pixels down.</param>
    /// <returns>Success, or AW-UIDRIVER-003 when there is no scroll viewer.</returns>
    internal static Result<Unit> Scroll(Visual visual, double dx, double dy)
    {
        var viewer = visual as ScrollViewer ?? visual.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() ?? visual.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (viewer is null)
        {
            return UiDriverErrors.NotActionable.With($"{visual.GetType().Name} is not in a scroll viewer and holds none");
        }

        var x = Math.Clamp(viewer.Offset.X + dx, 0, Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width));
        var y = Math.Clamp(viewer.Offset.Y + dy, 0, Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height));
        viewer.Offset = new Vector(x, y);
        return Unit.Value;
    }

    private static Result<Unit> Run(Action action)
    {
        action();
        return Unit.Value;
    }

    private static Result<Unit> Missing(Visual visual, string action) =>
        UiDriverErrors.NotActionable.With($"{visual.GetType().Name} can't {action.ToLowerInvariant()}");
}
