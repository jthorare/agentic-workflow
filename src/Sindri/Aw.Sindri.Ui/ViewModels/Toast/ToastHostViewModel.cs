namespace Aw.Sindri.Ui.ViewModels.Toast;

/// <summary>
/// The shell's one toast slot (todo spec §3.3, D11). A newer toast replaces an older one, so only the newest Undo works,
/// except that a pick's toast is never replaced: a newer toast waits behind it until its window closes. A pick counts down
/// and never pauses; a plain toast with Undo pauses while the pointer or focus is on it. Dismissing a pick's toast hides it
/// and nothing else: its window still runs to the end. The timer is the injected clock's, one tick a second; its callback
/// hops to the UI thread's context and a tick whose toast has gone is dropped (generation).
/// </summary>
internal sealed class ToastHostViewModel : ObservableObject
{
    /// <summary>How long a toast lasts when it names no window.</summary>
    internal static readonly TimeSpan DefaultFor = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _clock;
    private readonly bool _macOS;
    private readonly UiContext _ui = new();
    private readonly List<ToastMessage> _pending = [];
    private ToastMessage? _current;
    private bool Hidden { get; set; }
    private ITimer? _timer;
    private long _gen;
    private bool _pointerOver;
    private bool _focused;

    internal ToastHostViewModel(TimeProvider clock, bool? macOS = null)
    {
        _clock = clock;
        _macOS = macOS ?? OperatingSystem.IsMacOS();
    }

    /// <summary>Whether a toast is on screen (a dismissed pick's is not, though its window still runs).</summary>
    public bool IsShown => _current is not null && !Hidden;

    /// <summary>The toast's words.</summary>
    public string Text => _current?.Text ?? "";

    /// <summary>Whether the toast has an Undo button.</summary>
    public bool HasUndo => _current?.Undo is not null;

    /// <summary>The Undo button's words: the counting form for a toast with a countdown, else the form with the key.</summary>
    public string UndoLabel => _current switch
    {
        { CountdownSeconds: not null } => Labels.ToastUndoCountdown(Remaining),
        _ => Labels.ToastUndo(_macOS),
    };

    /// <summary>Seconds left in the toast's window.</summary>
    internal int Remaining { get; private set; }

    /// <summary>Whether the toast is a pick's, shown or hidden.</summary>
    internal bool IsPickOpen => _current is { Kind: ToastKind.Pick };

    /// <summary>Whether a toast is waiting behind a pick's.</summary>
    internal int PendingCount => _pending.Count;

    /// <summary>Whether the timer is stopped by the pointer or focus (a plain toast with Undo only).</summary>
    internal bool IsPaused => _current is { Kind: ToastKind.Normal, Undo: not null } && (_pointerOver || _focused);

    /// <summary>Whether the live region speaks; changes before <see cref="Announcement"/>.</summary>
    public bool IsAnnouncing { get; private set; }

    /// <summary>What the polite region says, once: the text, and the Undo key when there is Undo.</summary>
    public string Announcement { get; private set; } = "";

    /// <summary>Shows a toast: at once, unless a pick's window is open, when it waits behind it.</summary>
    /// <param name="toast">The toast.</param>
    internal void Show(ToastMessage toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        if (!IsPickOpen)
        {
            Start(toast);
            return;
        }

        // A newer toast replaces an older waiting one: a plain one is dropped, a pick's stays (it is never replaced).
        _ = _pending.RemoveAll(t => t.Kind == ToastKind.Normal);
        _pending.Add(toast);
    }

    /// <summary>The pointer is on the toast, or left it. The flags outlive a toast: the view says what is true when the next one shows.</summary>
    /// <param name="over">True while it is on.</param>
    internal void SetPointerOver(bool over) => _pointerOver = over;

    /// <summary>Focus is on the toast, or left it.</summary>
    /// <param name="focused">True while it is on.</param>
    internal void SetFocused(bool focused) => _focused = focused;

    /// <summary>Whether the visible toast has an Undo to run.</summary>
    internal bool CanUndo => IsShown && _current?.Undo is not null;

    /// <summary>Runs the visible toast's Undo and closes its window; false when nothing visible offers one.</summary>
    /// <returns>True when an Undo ran.</returns>
    internal async Task<bool> Undo()
    {
        if (!CanUndo || _current?.Undo is not { } undo)
        {
            return false;
        }

        End();
        string? failure;
        try
        {
            failure = await undo().ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            failure = e.Message;
        }

        if (failure is not null)
        {
            Show(new ToastMessage(Labels.ToastUndoFailed(failure)));
        }

        return true;
    }

    /// <summary>The Undo key (Ctrl+Z, ⌘Z): in a text field it is the field's own undo and does nothing of ours.</summary>
    /// <param name="inTextField">Whether a text field has focus.</param>
    /// <returns>True when it ran an Undo (the key is spent).</returns>
    internal bool PressUndoKey(bool inTextField)
    {
        if (inTextField || !CanUndo)
        {
            return false;
        }

        _ = Undo();
        return true;
    }

    /// <summary>Esc: dismisses the toast only while focus is on it. A plain toast goes; a pick's is hidden and its window runs on.</summary>
    /// <param name="focusOnToast">Whether focus is on the toast.</param>
    /// <returns>True when it dismissed one (the key is spent).</returns>
    internal bool PressEscape(bool focusOnToast)
    {
        if (!focusOnToast || !IsShown)
        {
            return false;
        }

        if (IsPickOpen)
        {
            Hidden = true;
            _pointerOver = false;
            _focused = false;
            Changed();
        }
        else
        {
            End();
        }

        return true;
    }

    private void Start(ToastMessage toast)
    {
        StopTimer();
        _current = toast;
        Hidden = false;
        Remaining = (int)(toast.CountdownSeconds is { } s ? s : DefaultFor.TotalSeconds);
        var gen = ++_gen;
        _timer = _clock.CreateTimer(_ => _ui.PostOrInline(() => Tick(gen)), state: null, _tick, _tick);
        Announce(Spoken(toast));
        Changed();
    }

    private string Spoken(ToastMessage toast)
    {
        if (toast.Undo is null)
        {
            return toast.Text;
        }

        var key = $"{toast.Text} {Labels.ToastSpokenKey(_macOS)}";
        return toast.Kind == ToastKind.Pick ? $"{key} {Labels.ToastSpokenWithin(Remaining)}" : key;
    }

    private void Tick(long gen)
    {
        if (gen != _gen || _current is null || IsPaused)
        {
            return;
        }

        Remaining--;
        if (Remaining <= 0)
        {
            End();
            return;
        }

        OnChanged(nameof(UndoLabel));
    }

    // Closes the slot, then shows the toast that waited behind a pick's window.
    private void End()
    {
        StopTimer();
        _gen++;
        _current = null;
        Hidden = false;
        Remaining = 0;
        IsAnnouncing = false;
        OnChanged(nameof(IsAnnouncing));
        Changed();
        if (_pending.Count > 0)
        {
            var next = _pending[0];
            _pending.RemoveAt(0);
            Start(next);
        }
    }

    // The flag changes before the text; the same words twice still change the text.
    private void Announce(string words)
    {
        IsAnnouncing = true;
        OnChanged(nameof(IsAnnouncing));
        Announcement = "";
        OnChanged(nameof(Announcement));
        Announcement = words;
        OnChanged(nameof(Announcement));
    }

    private void Changed()
    {
        OnChanged(nameof(IsShown));
        OnChanged(nameof(Text));
        OnChanged(nameof(HasUndo));
        OnChanged(nameof(UndoLabel));
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
