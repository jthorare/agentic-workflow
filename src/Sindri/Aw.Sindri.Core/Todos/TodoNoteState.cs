namespace Aw.Sindri.Core.Todos;

/// <summary>Where the note sync stands (the doctor row 'todo', todo spec §6; the panel's sync line).</summary>
public enum TodoNoteState
{
    /// <summary>No 'todo.obsidian' key: nothing runs.</summary>
    NotSet,

    /// <summary>The key is set and the passes run.</summary>
    Syncing,

    /// <summary>The key was set from the terminal and the note has open lines: nothing is written until 'sindri todo note import' (REQ-E6).</summary>
    WaitingForImport,

    /// <summary>The note is missing with a base, unreadable, 0 bytes with a base, or over 1 MB (REQ-S3).</summary>
    CantRead,

    /// <summary>The disk has no atomic swap: the note is read but never written, and bare lines aren't adopted.</summary>
    OneWay,

    /// <summary>Three swaps in one pass each found a new edit; the next 60 s check tries again.</summary>
    Busy,

    /// <summary>There is no profile yet, so there is no note to sync (#3113): not a failure, and no error to show.</summary>
    NotSetUp,
}
