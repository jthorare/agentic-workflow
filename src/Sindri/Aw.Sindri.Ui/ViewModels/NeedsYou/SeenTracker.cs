namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Which needs-you rows the user has seen (UI spec §5): presentation state, kept by row id, never the API's. A row is seen
/// once it has been on screen and focused for <see cref="OnScreenFor"/>, or acted on, or marked by Mark all seen. The ids
/// are kept in memory and in the preference file, and dropped when the row leaves the list. Timers run on the injected
/// clock; their callbacks arrive on a pool thread, so they hop to the UI thread's context (captured at construction) before
/// anything changes: a bound property or a view handler never runs on the timer's thread. A hop runs a turn later, so each
/// timer carries a generation and a callback whose timer was replaced or cancelled meanwhile is dropped.
/// </summary>
internal sealed partial class SeenTracker : ObservableObject
{
    /// <summary>How long a row must be on screen and focused to count as seen.</summary>
    internal static readonly TimeSpan OnScreenFor = TimeSpan.FromSeconds(1);

    /// <summary>How long Mark all seen offers Undo (was 10 s; q93 raised it so the offer outlasts a glance away).</summary>
    internal static readonly TimeSpan UndoFor = TimeSpan.FromMinutes(5);

    private readonly Preferences _preferences;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly List<string> _seen = [];
    private readonly Dictionary<string, ITimer> _watching = new(StringComparer.Ordinal);
    private List<string> _undo = [];
    private ITimer? _undoTimer;
    private readonly UiContext _ui = new();
    private readonly Dictionary<string, long> _dwellGen = new(StringComparer.Ordinal);
    private long _epoch;
    private long _undoGen;

    internal SeenTracker(Preferences preferences, TimeProvider clock)
    {
        _preferences = preferences;
        _clock = clock;
    }

    /// <summary>The ids seen, newest last.</summary>
    internal IReadOnlyList<string> Seen
    {
        get
        {
            lock (_gate)
            {
                return [.. _seen];
            }
        }
    }

    /// <summary>How many rows the last Mark all seen marked while its Undo is still offered; 0 when none is.</summary>
    internal int UndoCount
    {
        get
        {
            lock (_gate)
            {
                return _undo.Count;
            }
        }
    }

    /// <summary>Reads the seen ids the preference file holds.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are read (an unreadable file reads as none).</returns>
    internal async ValueTask Restore(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> stored;
        try
        {
            stored = (await _preferences.Load(cancellationToken).ConfigureAwait(true)).SeenIds;   // back on the caller (the UI thread): the change below is bound
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            stored = [];
        }

        lock (_gate)
        {
            _seen.Clear();
            _seen.AddRange(stored);
        }

        OnChanged(nameof(Seen));
    }

    /// <summary>Whether a row has been seen.</summary>
    /// <param name="id">The row's id.</param>
    /// <returns>True when it has.</returns>
    internal bool IsSeen(string id)
    {
        lock (_gate)
        {
            return _seen.Contains(id, StringComparer.Ordinal);
        }
    }

    /// <summary>Forgets the seen ids of rows that left the list (and stops watching them).</summary>
    /// <param name="current">The ids in the list now.</param>
    internal void Retain(IReadOnlyCollection<string> current)
    {
        bool changed;
        lock (_gate)
        {
            var keep = current.ToHashSet(StringComparer.Ordinal);
            changed = _seen.RemoveAll(id => !keep.Contains(id)) > 0;
            foreach (var gone in _watching.Keys.Where(id => !keep.Contains(id)).ToList())
            {
                _ = _dwellGen.Remove(gone);
                _ = _watching.Remove(gone, out var timer);
                timer!.Dispose(); // why: the key came from this dictionary a line above
            }

            _ = _undo.RemoveAll(id => !keep.Contains(id));
        }

        if (changed)
        {
            Persist();
            OnChanged(nameof(Seen));
        }
    }

    /// <summary>
    /// Tells the tracker whether a row is on screen and focused. Staying so for <see cref="OnScreenFor"/> marks it seen;
    /// losing either, or leaving, restarts the second.
    /// </summary>
    /// <param name="id">The row's id.</param>
    /// <param name="onScreenAndFocused">True while the row is visible in the focused window.</param>
    internal void Watch(string id, bool onScreenAndFocused)
    {
        lock (_gate)
        {
            if (!onScreenAndFocused)
            {
                if (_watching.Remove(id, out var stopped))
                {
                    stopped.Dispose();
                }

                return;
            }

            if (_watching.ContainsKey(id) || _seen.Contains(id, StringComparer.Ordinal))
            {
                return;
            }

            var gen = ++_epoch;
            _dwellGen[id] = gen;
            _watching[id] = _clock.CreateTimer(_ => _ui.PostOrInline(() => DwellElapsed(id, gen)), state: null, OnScreenFor, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Holds the Undo offer open while the user has the Undo button (focus or pointer); letting go gives it its full time again.</summary>
    /// <param name="holding">True while the button has focus or the pointer.</param>
    internal void HoldUndo(bool holding)
    {
        lock (_gate)
        {
            if (_undo.Count == 0)
            {
                return;
            }

            if (holding)
            {
                StopUndoTimer();
            }
            else if (_undoTimer is null)
            {
                StartUndoTimer();
            }
        }
    }

    /// <summary>Marks a row seen because the user acted on it.</summary>
    /// <param name="id">The row's id.</param>
    internal void Acted(string id) => MarkSeen(id);

    /// <summary>Marks every current row seen (none that arrive later) and offers Undo for <see cref="UndoFor"/>.</summary>
    /// <param name="current">The ids in the list now.</param>
    internal void MarkAllSeen(IReadOnlyCollection<string> current)
    {
        lock (_gate)
        {
            StopUndoTimer();
            _undo = [.. current.Distinct(StringComparer.Ordinal).Where(id => !_seen.Contains(id, StringComparer.Ordinal))];
            _seen.AddRange(_undo);
            foreach (var id in _undo)
            {
                if (_watching.Remove(id, out var timer))
                {
                    timer.Dispose();
                }
            }

            if (_undo.Count > 0)
            {
                StartUndoTimer();
            }
        }

        Persist();
        OnChanged(nameof(Seen));
        OnChanged(nameof(UndoCount));
    }

    /// <summary>Makes the rows the last Mark all seen marked unseen again, while its Undo is offered.</summary>
    /// <returns>True when Undo was offered and is done.</returns>
    internal bool Undo()
    {
        lock (_gate)
        {
            if (_undo.Count == 0)
            {
                return false;
            }

            _ = _seen.RemoveAll(id => _undo.Contains(id, StringComparer.Ordinal));
            _undo = [];
            StopUndoTimer();
        }

        Persist();
        OnChanged(nameof(Seen));
        OnChanged(nameof(UndoCount));
        return true;
    }

    private void MarkSeen(string id)
    {
        lock (_gate)
        {
            _ = _dwellGen.Remove(id);
            if (_watching.Remove(id, out var timer))
            {
                timer.Dispose();
            }

            if (_seen.Contains(id, StringComparer.Ordinal))
            {
                return;
            }

            _seen.Add(id);
        }

        Persist();
        OnChanged(nameof(Seen));
    }

    // Written over whatever the file holds by then, through the app's one write queue.
    private void Persist()
    {
        string[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _seen];
        }

        _ = _preferences.Update(p => p with { SeenIds = snapshot });
    }
}
