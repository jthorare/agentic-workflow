namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Where a change came from (the 'by' of todo spec §4.1, reduced to the two sides of the sync).</summary>
public enum NoteOrigin
{
    /// <summary>Sindri: the app, the CLI, an agent or the note service's own bookkeeping.</summary>
    Sindri,

    /// <summary>The note.</summary>
    Obsidian,
}
