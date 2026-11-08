namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Reads the Obsidian todo note (todo spec §4.5). Pure: no clock, no file.</summary>
public static class NoteParser
{
    /// <summary>Reads a note. A top-level '- [ ]', '- [x]' or '- [X]' line is a todo; task lines in a fenced block (backtick or tilde), in a quote or callout ('&gt;') or indented are not, and neither is any other check state ('[/]', '[-]'). Every other line is kept as it is.</summary>
    /// <param name="note">The note's text (any mix of "\n" and "\r\n").</param>
    /// <returns>The document.</returns>
    public static NoteDocument Parse(string note) => throw new NotSupportedException();
}
