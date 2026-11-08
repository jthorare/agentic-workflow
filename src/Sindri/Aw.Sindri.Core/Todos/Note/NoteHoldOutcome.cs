namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a pass did about the hold (todo spec §4.1 'todo.note.held' rules).</summary>
public enum NoteHoldOutcome
{
    /// <summary>No hold before, none now.</summary>
    None,

    /// <summary>A new hold opened.</summary>
    Opened,

    /// <summary>The same set of ids is still held: nothing to append.</summary>
    Unchanged,

    /// <summary>A different (smaller or changed) set replaces the open hold.</summary>
    Replaced,

    /// <summary>A hold was open and the count fell under the threshold: it dissolves.</summary>
    Dissolved,
}
