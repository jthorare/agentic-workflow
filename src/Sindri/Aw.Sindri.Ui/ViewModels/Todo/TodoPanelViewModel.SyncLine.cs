using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's sync line (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.1, §3.4): "Synced with Obsidian 10:05" when all is well, a warning with the reason when not, "Saving your pick to Obsidian
/// in 6 s" while a pick's Undo window runs, nothing when no note is set or an older Sindri has no note route. The warnings and the countdown are a polite live region; the quiet line is not.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>The quiet line ("Synced with Obsidian 10:05"); empty when a warning or the countdown shows or no note is set.</summary>
    public string SyncQuiet { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The live line: a warning or a pick's countdown; empty otherwise. Its region is always in the tree.</summary>
    public string SyncLive { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="SyncLive"/> is a warning (drawn in the warning text colour), false for a pick's countdown.</summary>
    public bool SyncIsWarning { get; private set => Set(ref field, value); }

    /// <summary>True when any sync line is drawn.</summary>
    public bool HasSync => SyncQuiet.Length > 0 || SyncLive.Length > 0;

    /// <summary>Reads the note's state for the sync line (on connect and after every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the line is shown.</returns>
    internal Task RefreshSyncLine(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Shows the sync line for a note read; null (no note route, or none read) hides it.</summary>
    /// <param name="note">The note's state, or null.</param>
    internal void ShowSync(TodoNoteView? note) => throw new NotSupportedException();
}
