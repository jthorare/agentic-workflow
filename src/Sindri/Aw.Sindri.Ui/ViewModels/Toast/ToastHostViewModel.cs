namespace Aw.Sindri.Ui.ViewModels.Toast;

#pragma warning disable MA0041, CA1822 // red stub: bodies throw until the green commit

/// <summary>
/// The shell's one toast slot (todo spec §3.3, D11). A newer toast replaces an older one, so only the newest Undo works,
/// except that a pick's toast is never replaced: a newer toast waits behind it until its window closes. A pick counts down
/// and never pauses; a plain toast with Undo pauses while the pointer or focus is on it.
/// </summary>
internal sealed class ToastHostViewModel : ObservableObject
{
    /// <summary>How long a toast lasts when it names no window.</summary>
    internal static readonly TimeSpan DefaultFor = TimeSpan.FromSeconds(10);

    internal ToastHostViewModel(TimeProvider clock, bool? macOS = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _ = macOS;
    }

    /// <summary>Whether a toast is on screen.</summary>
    public bool IsShown => throw new NotSupportedException();

    /// <summary>The toast's words.</summary>
    public string Text => throw new NotSupportedException();

    /// <summary>Whether the toast has an Undo button.</summary>
    public bool HasUndo => throw new NotSupportedException();

    /// <summary>The Undo button's words.</summary>
    public string UndoLabel => throw new NotSupportedException();

    /// <summary>Seconds left in the toast's window.</summary>
    internal int Remaining => throw new NotSupportedException();

    /// <summary>Whether the toast is a pick's, shown or hidden.</summary>
    internal bool IsPickOpen => throw new NotSupportedException();

    /// <summary>How many toasts wait behind a pick's.</summary>
    internal int PendingCount => throw new NotSupportedException();

    /// <summary>Whether the timer is stopped by the pointer or focus.</summary>
    internal bool IsPaused => throw new NotSupportedException();

    /// <summary>Whether the live region speaks.</summary>
    public bool IsAnnouncing => throw new NotSupportedException();

    /// <summary>What the polite region says, once.</summary>
    public string Announcement => throw new NotSupportedException();

    /// <summary>The toast's region name.</summary>
    public string RegionName => throw new NotSupportedException();

    /// <summary>The Undo button's name for a screen reader.</summary>
    public string UndoName => throw new NotSupportedException();

    /// <summary>Whether the visible toast has an Undo to run.</summary>
    internal bool CanUndo => throw new NotSupportedException();

    /// <summary>Shows a toast.</summary>
    /// <param name="toast">The toast.</param>
    internal void Show(ToastMessage toast) => throw new NotSupportedException();

    /// <summary>The pointer is on the toast, or left it.</summary>
    /// <param name="over">True while it is on.</param>
    internal void SetPointerOver(bool over) => throw new NotSupportedException();

    /// <summary>Focus is on the toast, or left it.</summary>
    /// <param name="focused">True while it is on.</param>
    internal void SetFocused(bool focused) => throw new NotSupportedException();

    /// <summary>Runs the visible toast's Undo.</summary>
    /// <returns>True when an Undo ran.</returns>
    internal Task<bool> Undo() => throw new NotSupportedException();

    /// <summary>The Undo key.</summary>
    /// <param name="inTextField">Whether a text field has focus.</param>
    /// <returns>True when it ran an Undo.</returns>
    internal bool PressUndoKey(bool inTextField) => throw new NotSupportedException();

    /// <summary>Esc.</summary>
    /// <param name="focusOnToast">Whether focus is on the toast.</param>
    /// <returns>True when it dismissed one.</returns>
    internal bool PressEscape(bool focusOnToast) => throw new NotSupportedException();
}
