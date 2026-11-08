namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Writes todos as note lines and rebuilds the note editing only the lines that need it (todo spec §4.5). Pure.</summary>
public static class NoteRenderer
{
    /// <summary>The line for a todo: '- [ ] text 📅 date [label](sindri://…) … ✅ date ^td-id', each part only when the todo has it. The text is escaped; each link's label is derived from the link ('sindri://reviews/o/r/12' is 'PR #12', 'sindri://linear/ENG-12' is 'ENG-12', 'sindri://item/ITM-1' is 'ITM-1'; any other link kind is not a todo link).</summary>
    /// <param name="todo">The todo.</param>
    /// <returns>The line without a line ending.</returns>
    public static string RenderLine(SindriTodo todo) => throw new NotSupportedException();

    /// <summary>Puts a backslash before each of `, &lt;, ![[, [[, %% and $= in text (checked in that order of length, "![[" before "[[", and a match is consumed whole).</summary>
    /// <param name="text">Plain text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text) => throw new NotSupportedException();

    /// <summary>Takes a backslash off each of the sequences <see cref="Escape"/> escapes; any other backslash stays.</summary>
    /// <param name="text">Escaped text.</param>
    /// <returns>The plain text.</returns>
    public static string Unescape(string text) => throw new NotSupportedException();

    /// <summary>Applies edits to a note, leaving every other line byte for byte: Replace and Remove by number, Append after the document's last todo line (of the document as parsed, before the edits), in the order given; with no todo line they go at the end, after adding a line ending to a last line that has none. New lines use the note's first line ending ("\n" when it has none). A document with no lines becomes '# Todo', a blank line, then the appended lines.</summary>
    /// <param name="document">The parsed note.</param>
    /// <param name="edits">The edits.</param>
    /// <returns>The new text.</returns>
    public static string Rebuild(NoteDocument document, IReadOnlyList<NoteLineEdit> edits) => throw new NotSupportedException();
}
