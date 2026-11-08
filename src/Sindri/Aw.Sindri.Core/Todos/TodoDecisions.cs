using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>The pure decisions of the todo writes (todo spec §4.4, §6): whether a write may touch a row, and what a state verb needs the row to be.</summary>
public static class TodoDecisions
{
    /// <summary>The SND-TODO-409 text for a stale version.</summary>
    public const string StaleText = "That todo changed somewhere else. Reload and try again.";

    /// <summary>The SND-TODO-423 text.</summary>
    public const string LockedText = "That todo changed in Sindri and in Obsidian. Pick which version to keep first.";

    /// <summary>Whether a write may touch the todo.</summary>
    /// <param name="id">The todo the caller named.</param>
    /// <param name="row">Its row, or null when there is none.</param>
    /// <param name="seen">The version the caller saw, or null to use the current one.</param>
    /// <returns>The row, SND-TODO-404 for no row, SND-TODO-423 for an open conflict, or SND-TODO-409 for a stale version.</returns>
    public static Result<TodoRow> Writable(TodoId id, TodoRow? row, long? seen) =>
        row is null ? SindriErrors.TodoNotFound.With($"No todo {id.Value}.")
        : row.Conflict is not null ? SindriErrors.TodoLocked.With(LockedText)
        : seen is { } version && version != row.Version ? SindriErrors.TodoConflict.With(StaleText)
        : row;

    /// <summary>Whether an edit changes anything.</summary>
    /// <param name="text">New text, or null.</param>
    /// <param name="due">A new due date, or null.</param>
    /// <param name="clearDue">Whether the due date is dropped.</param>
    /// <param name="links">The new whole list of links, or null.</param>
    /// <returns>SND-TODO-001 when no field is given, else null.</returns>
    public static Error? NoEdit(string? text, DateOnly? due, bool clearDue, IReadOnlyList<string>? links) =>
        text is null && due is null && !clearDue && links is null ? SindriErrors.TodoText.With("change needs a version and at least one of text, due, links.") : null;

    /// <summary>Whether <paramref name="verb"/> fits the row's state: done and remove need an open todo, reopen a done one, restore a removed one.</summary>
    /// <param name="row">The todo.</param>
    /// <param name="verb">The verb.</param>
    /// <returns>Unit, or SND-TODO-409 naming the state it is in.</returns>
    public static Result<Unit> Fits(TodoRow row, TodoVerb verb)
    {
        ArgumentNullException.ThrowIfNull(row);
        var fits = verb switch
        {
            TodoVerb.Done or TodoVerb.Remove => IsOpen(row),
            TodoVerb.Reopen => !row.Removed && row.DoneAt is not null,
            TodoVerb.Restore => row.Removed,
            _ => false,
        };
        return fits ? Unit.Value : SindriErrors.TodoConflict.With($"{row.Id.Value} is {StateOf(row)}, so it can't be {Past(verb)}.");
    }

    /// <summary>Whether the todo is open: not done and not removed.</summary>
    /// <param name="row">The todo.</param>
    /// <returns>True when open.</returns>
    public static bool IsOpen(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return !row.Removed && row.DoneAt is null;
    }

    private static string StateOf(TodoRow row) => row.Removed ? "removed" : row.DoneAt is not null ? "done" : "open";

    private static string Past(TodoVerb verb) => verb switch
    {
        TodoVerb.Done => "checked off",
        TodoVerb.Reopen => "reopened",
        TodoVerb.Remove => "removed",
        TodoVerb.Restore => "restored",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "not a todo verb"),
    };
}
