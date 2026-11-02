using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Aw.UiDriver.Avalonia;

/// <summary>
/// The popups that are open now. Avalonia has no public list of them, and a popup drawn in its own popup window (a menu flyout, a context menu, a combo box
/// list on X11, Win32 and macOS) is not in the window's visual tree, so the driver follows <see cref="Popup.IsOpenProperty"/> itself. Read and written on
/// the UI thread only.
/// </summary>
internal static class OpenPopups
{
    private static readonly Lock _gate = new();
    private static readonly List<(int Sequence, Popup Popup)> _open = [];
    private static int _next;
    private static bool _tracking;

    /// <summary>Starts following popups; a second call does nothing. Popups that opened before this call are not seen.</summary>
    internal static void Track()
    {
        lock (_gate)
        {
            if (_tracking)
            {
                return;
            }

            _tracking = true;
        }

        _ = Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, _) => Changed(popup));
    }

    /// <summary>The content of each popup open now that is drawn in its own popup window (not the window's overlay layer) and was opened from <paramref name="owner"/> (found from the popup's host, so a flyout or context menu with only a logical parent counts, and a submenu counts for the window its parent popup was opened from), oldest first, with the number its node ids use.</summary>
    /// <param name="owner">The window whose popups are wanted; a popup opened from another window is left out.</param>
    /// <returns>A snapshot.</returns>
    internal static IReadOnlyList<(int Sequence, Visual Content)> Current(Window owner)
    {
        List<(int, Visual)> found = [];
        lock (_gate)
        {
            foreach (var (sequence, popup) in _open)
            {
                if (popup.IsOpen && !popup.IsUsingOverlayLayer && ReferenceEquals(OwnerWindow(popup), owner) && popup.Child is { } content)
                {
                    found.Add((sequence, content));
                }
            }
        }

        return found;
    }

    // Flyouts and context menus create their Popup in code with only a logical parent, so the popup itself has no top level. Popup.Host is internal, so the public way to it is the content: once open, the content sits in the
    // popup's host and its top level is the PopupRoot, whose ParentTopLevel is the window it was opened from; a submenu's root has the parent popup's root there, so follow the roots up.
    private static TopLevel? OwnerWindow(Popup popup)
    {
        var top = popup.Child is { } child && TopLevel.GetTopLevel(child) is { } hostRoot ? hostRoot : TopLevel.GetTopLevel(popup.PlacementTarget ?? popup);
        while (top is PopupRoot parent)
        {
            top = parent.ParentTopLevel;
        }

        return top;
    }

    private static void Changed(Popup popup)
    {
        lock (_gate)
        {
            _ = _open.RemoveAll(o => ReferenceEquals(o.Popup, popup));
            if (popup.IsOpen)
            {
                _open.Add((_next++, popup));
            }
        }
    }
}
