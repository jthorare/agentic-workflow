using Avalonia;
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

    /// <summary>The content of each popup open now that is drawn in its own popup window (not the window's overlay layer), oldest first, with the number its node ids use.</summary>
    /// <returns>A snapshot.</returns>
    internal static IReadOnlyList<(int Sequence, Visual Content)> Current()
    {
        List<(int, Visual)> found = [];
        lock (_gate)
        {
            foreach (var (sequence, popup) in _open)
            {
                if (popup.IsOpen && !popup.IsUsingOverlayLayer && popup.Child is { } content)
                {
                    found.Add((sequence, content));
                }
            }
        }

        return found;
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
