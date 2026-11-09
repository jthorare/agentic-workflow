namespace Aw.Sindri.Core.Events;

/// <summary>The todo event types (todo spec §4.1), split out of EventTypes for its file-length limit.</summary>
public static partial class EventTypes
{
    /// <summary>A todo was added (todo spec §4.1).</summary>
    public const string TodoAdded = "todo.added";

    /// <summary>A todo's text, due date or links were edited.</summary>
    public const string TodoChanged = "todo.changed";

    /// <summary>A todo was checked off.</summary>
    public const string TodoDone = "todo.done";

    /// <summary>A done todo was reopened.</summary>
    public const string TodoReopened = "todo.reopened";

    /// <summary>A todo was deleted (soft).</summary>
    public const string TodoRemoved = "todo.removed";

    /// <summary>A removed todo came back.</summary>
    public const string TodoRestored = "todo.restored";

    /// <summary>A sync found a todo changed in Sindri and in the Obsidian note.</summary>
    public const string TodoConflicted = "todo.conflicted";

    /// <summary>A caller picked a side of an open both-places conflict.</summary>
    public const string TodoConflictResolved = "todo.conflict.resolved";

    /// <summary>A conflict pick was undone, so the conflict is open again.</summary>
    public const string TodoConflictReopened = "todo.conflict.reopened";

    /// <summary>A sync pass held the note's changes for the owner to apply or put back.</summary>
    public const string TodoNoteHeld = "todo.note.held";

    /// <summary>A caller picked what happens to the held changes.</summary>
    public const string TodoNoteHeldResolved = "todo.note.held.resolved";

    /// <summary>A hold pick was undone, so the hold is open again.</summary>
    public const string TodoNoteHeldReopened = "todo.note.held.reopened";
}
