using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>What the panel's sync line says for a note read (todo spec §3.4): one of the states of the doctor row, a hold, lines that can't sync, a pick's countdown, or all well.</summary>
internal static class TodoSyncLine
{
    /// <summary>The line for a note.</summary>
    /// <param name="note">The note's state, or null when none is set or the route isn't served.</param>
    /// <param name="now">The time now.</param>
    /// <param name="zone">The owner's time zone, for the last sync's clock time.</param>
    /// <returns>The quiet text, the live text and whether the live text is a warning; all empty draws no line.</returns>
    internal static (string Quiet, string Live, bool IsWarning) Of(TodoNoteView? note, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (note is null || string.Equals(note.State, "not set", StringComparison.Ordinal) || string.Equals(note.State, "not set up", StringComparison.Ordinal))
        {
            return (string.Empty, string.Empty, false);
        }

        return Warning(note) is { } warning ? (string.Empty, warning, true) : Settled(note, now, zone);
    }

    /// <summary>The whole seconds left of the nearest pick's Undo window, rounded up; null when no window is open.</summary>
    /// <param name="note">The note's state.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The seconds, or null.</returns>
    internal static int? Countdown(TodoNoteView note, DateTimeOffset now)
    {
        var open = note.Picks.Select(p => p.UndoUntil - now).Where(left => left > TimeSpan.Zero).ToList();
        return open.Count == 0 ? null : (int)Math.Ceiling(open.Min().TotalSeconds);
    }

    // The states that are a warning by themselves, and the warnings a syncing note carries (a hold first, then lines that can't sync).
    private static string? Warning(TodoNoteView note) => note.State switch
    {
        "waiting for import" => Labels.TodoSyncWaiting,
        "one way" => Labels.TodoSyncOneWay,
        "can't read" => Labels.TodoSyncCantRead,
        "note busy" => Labels.TodoSyncBusy,
        _ => note.Hold is { } hold ? Labels.TodoSyncPaused(hold.Changes.Count) : note.CantSync.Count > 0 ? Labels.TodoSyncCantSync(note.CantSync) : null,
    };

    private static (string Quiet, string Live, bool IsWarning) Settled(TodoNoteView note, DateTimeOffset now, TimeZoneInfo zone) =>
        Countdown(note, now) is { } seconds
            ? (string.Empty, Labels.TodoSyncSavingPick(seconds), false)
            : (Labels.TodoSyncSynced(note.LastSyncAt is { } at ? TimeZoneInfo.ConvertTime(at, zone) : null), string.Empty, false);
}
