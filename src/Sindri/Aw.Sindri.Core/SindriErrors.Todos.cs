using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-TODO-001: a todo's text is empty or over 280 characters, a rule the MCP schema can't say, or a bad client id (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoText = new("SND-TODO-001", ErrorKind.Invalid, "A todo is one line of 1 to 280 characters.");

    /// <summary>SND-TODO-002: a bad 'todo.obsidian' key (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoNoteKey = new("SND-TODO-002", ErrorKind.Invalid, "The todo note must be a .md file inside one of your library vaults. Pick one in Settings → Connections → Obsidian todo note (todo.obsidian).");

    /// <summary>SND-TODO-003: a bad date or due word (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoDate = new("SND-TODO-003", ErrorKind.Invalid, "Dates are YYYY-MM-DD, or \"\" to clear.");

    /// <summary>SND-TODO-004: a link of no known shape, a PR url on another host, or more than 10 links (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoLink = new("SND-TODO-004", ErrorKind.Invalid, "That link isn't an item, PR, Linear key or workflow.");

    /// <summary>SND-TODO-005: nothing to sort out, nothing to undo, or no changes waiting in the note (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoNothingToPick = new("SND-TODO-005", ErrorKind.Conflict, "That todo has nothing to sort out.");

    /// <summary>SND-TODO-006: adding past 500 open todos (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoCap = new("SND-TODO-006", ErrorKind.Conflict, "You have 500 open todos. Finish or remove some first.");

    /// <summary>SND-TODO-007: a forbidden character, a note marker or a markdown link in the text (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoForbidden = new("SND-TODO-007", ErrorKind.Invalid, "A todo can't contain 📅, ✅, ^td- or a markdown link: those belong to the note.");

    /// <summary>SND-TODO-008: 'import' with nothing to import (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoNothingToImport = new("SND-TODO-008", ErrorKind.Conflict, "The note has no open tasks to import.");

    /// <summary>SND-TODO-010: an id prefix that matches several todos; the CLI only (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoAmbiguous = new("SND-TODO-010", ErrorKind.Invalid, "That id prefix matches several todos.");

    /// <summary>SND-TODO-404: no such todo (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoNotFound = new("SND-TODO-404", ErrorKind.NotFound, "No such todo.");

    /// <summary>SND-TODO-409: the todo changed since the caller read it, or the note moved since the caller's view (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoConflict = new("SND-TODO-409", ErrorKind.Conflict, "That todo changed somewhere else. Reload and try again.");

    /// <summary>SND-TODO-423: a write to a todo with an open both-places conflict; answered with HTTP 423 (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoLocked = new("SND-TODO-423", ErrorKind.Conflict, "That todo changed in Sindri and in Obsidian. Pick which version to keep first.");

    /// <summary>SND-TODO-429: the 21st add in an hour from one session, or from one piped CLI caller (todo spec §6).</summary>
    public static readonly ErrorDefinition TodoRateLimited = new("SND-TODO-429", ErrorKind.RateLimited, "An agent can add 20 todos an hour.");

    /// <summary>The todo codes (todo spec §6), for <see cref="All"/>.</summary>
    private static ErrorDefinition[] EveryTodo() =>
        [TodoText, TodoNoteKey, TodoDate, TodoLink, TodoNothingToPick, TodoCap, TodoForbidden, TodoNothingToImport, TodoAmbiguous, TodoNotFound, TodoConflict, TodoLocked, TodoRateLimited];
}
