namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A parsed note: every line, each kept as it was, with the todos marked.</summary>
/// <param name="Lines">The lines in file order. A final line ending doesn't add an empty last line.</param>
public sealed record NoteDocument(IReadOnlyList<NoteLine> Lines)
{
    /// <summary>The todo lines in file order.</summary>
    public IEnumerable<NoteTodoLine> Todos => Lines.Where(static l => l.Todo is not null).Select(static l => l.Todo!); // why: filtered on the line before

    /// <summary>The note as text, every line with its own ending: byte for byte what <see cref="NoteParser.Parse"/> read.</summary>
    /// <returns>The text.</returns>
    public string Render() => string.Concat(Lines.Select(static l => l.Text + l.Eol));
}
