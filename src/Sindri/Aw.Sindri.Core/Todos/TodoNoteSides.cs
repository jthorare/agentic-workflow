using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>What a pick compares and applies: the note's line for a todo read as a todo's fields (todo spec D11, §4.5 "The hold").</summary>
public static class TodoNoteSides
{
    /// <summary>The note's version of a todo, read from its first line carrying the id.</summary>
    /// <param name="note">The note.</param>
    /// <param name="id">The todo.</param>
    /// <returns>The side, with 'Removed' true when no line carries the id.</returns>
    public static TodoSide Of(NoteDocument note, TodoId id)
    {
        ArgumentNullException.ThrowIfNull(note);
        return note.Todos.FirstOrDefault(t => t.Id == id) is { } line ? FromLine(line.Raw, []) : new TodoSide(Text: null, Due: null, Links: [], Done: false, Removed: true);
    }

    /// <summary>A note line read as a side. The links the note can't show (kept on the todo only) are carried over from <paramref name="keep"/>.</summary>
    /// <param name="raw">The line.</param>
    /// <param name="keep">The todo's current links.</param>
    /// <returns>The side.</returns>
    public static TodoSide FromLine(string raw, IReadOnlyList<string> keep)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(keep);
        var line = NoteTodoLineReader.Read(0, raw);
        return new TodoSide(line.Text, line.Due, [.. line.Links, .. keep.Where(static l => !NoteLinks.IsTodoLink(l))], line.Done, Removed: false);
    }

    /// <summary>Whether the note still shows the Obsidian side of a conflict: the same text, due date, done state and links the note can show (a removed side needs a missing line).</summary>
    /// <param name="shown">The side the conflict stored.</param>
    /// <param name="now">The note's side now.</param>
    /// <returns>True when they agree.</returns>
    public static bool Same(TodoSide shown, TodoSide now)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(now);
        return shown.Removed || now.Removed
            ? shown.Removed == now.Removed
            : string.Equals(shown.Text, now.Text, StringComparison.Ordinal) && shown.Due == now.Due && shown.Done == now.Done
                && shown.Links.Where(NoteLinks.IsTodoLink).SequenceEqual(now.Links.Where(NoteLinks.IsTodoLink), StringComparer.Ordinal);
    }

    /// <summary>A todo's row read as a side (what Undo puts back).</summary>
    /// <param name="row">The row.</param>
    /// <returns>The side.</returns>
    public static TodoSide Of(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new TodoSide(row.Text, row.Due, row.Links, row.DoneAt is not null, row.Removed);
    }
}
