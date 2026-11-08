namespace Aw.Sindri.Core.Todos.Note;

internal sealed partial class NotePass
{
    /// <summary>The line for a todo Sindri changed: the owner's own text stays as typed until Sindri changes the text (an adopted `[[link]]` is not escaped by a tick).</summary>
    private static string SindriLine(NoteTodoLine line, SindriTodo todo) =>
        string.Equals(todo.Text, line.Text, StringComparison.Ordinal)
            ? NoteRenderer.RenderLine(todo, NoteTodoLineReader.RawTextOf(line.Raw))
            : NoteRenderer.RenderLine(todo);

    /// <summary>The line to save as base for a note line: the note's own text when the scrubber left it alone, else the line written from the scrubbed fields.</summary>
    private static string LineOf(NoteTodoLine line, TodoId id, NoteFields note) =>
        string.Equals(note.Text, line.Text, StringComparison.Ordinal) ? line.Raw : Render(id, note);

    private static string Render(TodoId id, NoteFields note) =>
        NoteRenderer.RenderLine(new SindriTodo(id, note.Text, note.Due, note.Links, note.Done, note.DoneOn));

    /// <summary>The owner's line with its id (a bad or repeated one, or none) replaced by the minted one, the rest kept as typed.</summary>
    private static string WithId(NoteTodoLine line, TodoId id)
    {
        var raw = line.Raw.TrimEnd();
        if (line.Id is not null || line.RejectedBlock is not null)
        {
            raw = raw[..raw.AsSpan().LastIndexOfAny(" \t")].TrimEnd();
        }

        return raw + " " + NoteTodoLineReader.IdMarker + id.Value["td_".Length..];
    }
}
