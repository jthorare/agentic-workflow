using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The Needs-you panel's presentation state (UI spec §5): each row's look, the header's single pulse when rows arrive,
/// Mark all seen and its Undo, and the reduced-motion switch (the OS setting or the in-app switch). No attention band
/// (screens spec R8-13). Built on <see cref="NeedsYouEntry"/>, so it doesn't wait for the API's row.
/// </summary>
internal sealed class NeedsYouSeenViewModel : ObservableObject
{
    private readonly SeenTracker _tracker;
    private readonly IMotionPreference _motion;
    private readonly TimeProvider _clock;
    private readonly Preferences _preferences;
    private readonly Dictionary<string, NeedsYouRowViewModel> _byId = new(StringComparer.Ordinal);
    private List<NeedsYouEntry> _entries = [];
    private HashSet<string> _arrivals = new(StringComparer.Ordinal);
    private bool _shown;
    private bool _restored;
    private string? _hintRow;

    private bool InApp { get; set; }

    private readonly UiContext _ui = new();

    internal NeedsYouSeenViewModel(Preferences preferences, IMotionPreference motion, TimeProvider clock)
    {
        (_preferences, _motion, _clock) = (preferences, motion, clock);
        _tracker = new SeenTracker(preferences, clock);
        _tracker.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SeenTracker.UndoCount), StringComparison.Ordinal))
            {
                OnChanged(nameof(UndoOffered));
                OnChanged(nameof(UndoText));
                OnChanged(nameof(UndoAnnouncement));
            }

            Reapply();
        };
        // The OS setting is polled on a timer's thread: the rows and what is bound to them change on the UI thread.
        _motion.Changed += (_, _) => _ui.PostOrInline(() =>
        {
            OnChanged(nameof(ReducedMotion));
            Reapply();
        });
    }

    /// <summary>The rows, in the order given.</summary>
    internal IReadOnlyList<NeedsYouRowViewModel> Rows { get; private set; } = [];

    /// <summary>True when the OS asks for reduced motion or the in-app switch is on (the switch forces it whatever the OS says).</summary>
    internal bool ReducedMotion => InApp || _motion.ReduceMotion;

    /// <summary>How many times the panel header has pulsed (once for each batch of arrivals, never under reduced motion).</summary>
    internal int HeaderPulses { get; private set; }

    /// <summary>True while Mark all seen offers Undo.</summary>
    internal bool UndoOffered => _tracker.UndoCount > 0;

    /// <summary>The Undo line, "N marked seen"; empty when none is offered.</summary>
    internal string UndoText => UndoOffered ? Labels.MarkedSeen(_tracker.UndoCount) : "";

    /// <summary>The polite live region's text: the Undo line while it is offered (the flag, <see cref="UndoOffered"/>, changes first), else the resting text, never empty.</summary>
    internal string UndoAnnouncement => UndoOffered ? UndoText : Labels.Sindri;

    /// <summary>Reads the seen ids and the in-app Reduce motion switch from the preference file.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when they are applied.</returns>
    internal async Task Restore(CancellationToken cancellationToken)
    {
        await _tracker.Restore(cancellationToken).ConfigureAwait(true);
        try
        {
            InApp = (await _preferences.Load(cancellationToken).ConfigureAwait(true)).ReduceMotion;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            InApp = false;
        }

        try
        {
            _hintRow = (await _preferences.Load(cancellationToken).ConfigureAwait(true)).TakeoverHintRow;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _hintRow = null;
        }

        _restored = true;
        OnChanged(nameof(ReducedMotion));
        Reapply();
        PlaceTakeoverHint();
    }

    /// <summary>Follows Settings' in-app Reduce motion switch.</summary>
    /// <param name="reduce">The switch's value.</param>
    internal void SetInAppReduceMotion(bool reduce)
    {
        InApp = reduce;
        OnChanged(nameof(ReducedMotion));
        Reapply();
    }

    /// <summary>
    /// Takes the list as the API serves it now: forgets the seen state of rows that left, makes a row of each new id (it
    /// slides in unless the list is the first shown or motion is reduced) and pulses the header once for the whole batch.
    /// </summary>
    /// <param name="entries">The rows to show, in order.</param>
    /// <param name="known">Every id the list has, shown or not (Home's filter, the hold): the seen state of those is kept. Null: the rows given.</param>
    internal void Update(IReadOnlyList<NeedsYouEntry> entries, IReadOnlyCollection<string>? known = null)
    {
        var ids = entries.Select(e => e.Id).ToList();
        var keep = known ?? ids;
        var fresh = ids.Where(id => !_byId.ContainsKey(id)).ToHashSet(StringComparer.Ordinal);
        _tracker.Retain(keep);
        foreach (var gone in _byId.Keys.Except(keep, StringComparer.Ordinal).ToList())
        {
            _ = _byId.Remove(gone);
        }

        _entries = [.. entries];
        _arrivals = _shown ? fresh : [];
        _shown = true;
        var reduced = ReducedMotion;
        foreach (var entry in _entries)
        {
            if (fresh.Contains(entry.Id))
            {
                _byId[entry.Id] = new NeedsYouRowViewModel(entry, _tracker.IsSeen(entry.Id), reduced, _arrivals.Contains(entry.Id), _clock);
            }
            else
            {
                _byId[entry.Id].Replace(entry);
            }
        }

        Rows = [.. _entries.Select(e => _byId[e.Id])];
        Reapply();
        PlaceTakeoverHint();
        OnChanged(nameof(Rows));
        if (fresh.Count > 0 && !reduced)
        {
            HeaderPulses++;
            OnChanged(nameof(HeaderPulses));
        }
    }

    /// <summary>Tells the tracker whether a row is on screen and focused.</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="onScreenAndFocused">True while it is.</param>
    internal void Watch(string id, bool onScreenAndFocused) => _tracker.Watch(id, onScreenAndFocused);

    /// <summary>Marks a row seen because the user acted on it.</summary>
    /// <param name="id">The row's id.</param>
    internal void Acted(string id) => _tracker.Acted(id);

    /// <summary>Marks every row now in the list seen, and offers Undo.</summary>
    internal void MarkAllSeen() => _tracker.MarkAllSeen([.. _entries.Select(e => e.Id)]);

    /// <summary>Holds the Undo offer open while the user is on its button (focus or pointer).</summary>
    /// <param name="holding">True while they are.</param>
    internal void HoldUndo(bool holding) => _tracker.HoldUndo(holding);

    /// <summary>Takes back the last Mark all seen, while its Undo is offered.</summary>
    internal void Undo() => _ = _tracker.Undo();

    // Rows follow the tracker and the motion setting. A row that arrived in the latest update stays "arrived" until the next.
    private void Reapply()
    {
        var reduced = ReducedMotion;
        foreach (var row in Rows)
        {
            row.Apply(_tracker.IsSeen(row.Id), reduced, _arrivals.Contains(row.Id));
        }
    }

    // The takeover hint goes under the first waiting-skill row the owner ever meets and stays under that row, whatever comes later: its id
    // is remembered in the preferences. Nothing is claimed before the stored one is read.
    private void PlaceTakeoverHint()
    {
        if (!_restored)
        {
            return;
        }

        if (_hintRow is null && Rows.FirstOrDefault(r => r.IsSkillWaiting) is { } first)
        {
            var id = _hintRow = first.Id;
            _ = _preferences.Update(p => p with { TakeoverHintRow = p.TakeoverHintRow ?? id });
        }

        foreach (var row in Rows)
        {
            row.ShowsTakeoverHint = string.Equals(row.Id, _hintRow, StringComparison.Ordinal);
        }
    }
}
