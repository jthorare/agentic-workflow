using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's sync line (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.1, §3.4): "Synced with Obsidian 10:05" when all is well, a warning with the reason when not, "Saving your pick to Obsidian
/// in 6 s" while a pick's Undo window runs, nothing when no note is set or an older Sindri has no note route. The warnings and the countdown are a polite live region; the quiet line is not.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    private TodoNoteView? _syncNote;
    private ITimer? _syncTick;
    private bool _profileMissing;

    /// <summary>Takes whether there is no profile yet: no vault is set up then, so the sync line stays empty whatever the note read says.</summary>
    /// <param name="missing">True on a home with no profile.</param>
    internal void SetProfileMissing(bool missing)
    {
        if (_profileMissing != missing)
        {
            _profileMissing = missing;
            ShowSync(_syncNote);
        }
    }

    /// <summary>The quiet line ("Synced with Obsidian 10:05"); empty when a warning or the countdown shows or no note is set.</summary>
    public string SyncQuiet { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The live line: a warning or a pick's countdown; empty otherwise. Its region is always in the tree.</summary>
    public string SyncLive { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="SyncLive"/> is a warning (drawn in the warning text colour), false for a pick's countdown.</summary>
    public bool SyncIsWarning { get; private set => Set(ref field, value); }

    /// <summary>True when any sync line is drawn.</summary>
    public bool HasSync => SyncQuiet.Length > 0 || SyncLive.Length > 0;

    /// <summary>Shows the sync line for a note read; null (no note route, or none read) hides it.</summary>
    /// <param name="note">The note's state, or null.</param>
    internal void ShowSync(TodoNoteView? note)
    {
        _syncTick?.Dispose();
        _syncTick = null;
        _syncNote = note;
        var now = _clock.GetUtcNow();
        (SyncQuiet, SyncLive, SyncIsWarning) = _profileMissing ? (string.Empty, string.Empty, false) : TodoSyncLine.Of(note, now, _clock.LocalTimeZone);
        OnChanged(nameof(HasSync));
        if (note is not null && TodoSyncLine.Countdown(note, now) is not null)
        {
            _syncTick = _clock.CreateTimer(_ => _ui.PostOrInline(() => ShowSync(_syncNote)), state: null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);   // the countdown runs on the clock, the window ends by itself
        }
    }
}
