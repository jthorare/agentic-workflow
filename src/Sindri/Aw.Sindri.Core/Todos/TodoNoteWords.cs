namespace Aw.Sindri.Core.Todos;

/// <summary>The words for where the note sync stands (todo spec §6): the route, the CLI and the doctor row say the same ones.</summary>
public static class TodoNoteWords
{
    /// <summary>The words for <paramref name="state"/>.</summary>
    /// <param name="state">Where the sync stands.</param>
    /// <returns>'not set', 'not set up' (no profile yet), 'syncing', 'waiting for import', 'can't read', 'one way' or 'note busy'.</returns>
    public static string Of(TodoNoteState state) => state switch
    {
        TodoNoteState.NotSet => "not set",
        TodoNoteState.NotSetUp => "not set up",
        TodoNoteState.Syncing => "syncing",
        TodoNoteState.WaitingForImport => "waiting for import",
        TodoNoteState.CantRead => "can't read",
        TodoNoteState.OneWay => "one way",
        TodoNoteState.Busy => "note busy",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a note state"),
    };
}
