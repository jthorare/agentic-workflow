using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Writes todos as note lines and rebuilds the note editing only the lines that need it (todo spec §4.5). Pure.</summary>
public static class NoteRenderer
{
    /// <summary>The sequences that run or embed in Obsidian, longest first so a match is consumed whole.</summary>
    private static readonly string[] _escaped = ["![[", "[[", "%%", "$=", "`", "<"];

    /// <summary>The line for a todo: '- [ ] text 📅 date [label](sindri://…) … ✅ date ^td-id', each part only when the todo has it. The text is escaped; each link's label is derived from the link ('sindri://reviews/o/r/12' is 'PR #12', 'sindri://linear/ENG-12' is 'ENG-12', 'sindri://item/ITM-1' is 'ITM-1'; any other link kind is not a todo link).</summary>
    /// <param name="todo">The todo.</param>
    /// <returns>The line without a line ending.</returns>
    public static string RenderLine(SindriTodo todo)
    {
        ArgumentNullException.ThrowIfNull(todo);
        var line = new StringBuilder(todo.Done ? "- [x] " : "- [ ] ").Append(Escape(todo.Text));
        if (todo.Due is { } due)
        {
            _ = line.Append(' ').Append(NoteTodoLineReader.DueMarker).Append(' ').Append(Format(due));
        }

        foreach (var link in NoteLinks.Renderable(todo.Links))
        {
            _ = line.Append(" [").Append(NoteLinks.Label(link)).Append("](").Append(link).Append(')');
        }

        if (todo is { Done: true, DoneOn: { } doneOn })
        {
            _ = line.Append(' ').Append(NoteTodoLineReader.DoneMark).Append(' ').Append(Format(doneOn));
        }

        return line.Append(' ').Append(NoteTodoLineReader.IdMarker).Append(todo.Id.Value.AsSpan("td_".Length)).ToString();
    }

    /// <summary>Puts a backslash before each of `, &lt;, ![[, [[, %% and $= in text (checked in that order of length, "![[" before "[[", and a match is consumed whole).</summary>
    /// <param name="text">Plain text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        var at = 0;
        while (at < text.Length)
        {
            var sequence = SequenceAt(text, at);
            if (sequence is null)
            {
                _ = result.Append(text[at++]);
                continue;
            }

            _ = result.Append('\\').Append(sequence);
            at += sequence.Length;
        }

        return result.ToString();
    }

    /// <summary>Takes a backslash off each of the sequences <see cref="Escape"/> escapes; any other backslash stays.</summary>
    /// <param name="text">Escaped text.</param>
    /// <returns>The plain text.</returns>
    public static string Unescape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        var at = 0;
        while (at < text.Length)
        {
            var sequence = text[at] == '\\' ? SequenceAt(text, at + 1) : null;
            if (sequence is null)
            {
                _ = result.Append(text[at++]);
                continue;
            }

            _ = result.Append(sequence);
            at += 1 + sequence.Length;
        }

        return result.ToString();
    }

    /// <summary>Applies edits to a note, leaving every other line byte for byte: Replace and Remove by number, Append after the document's last todo line (of the document as parsed, before the edits), in the order given; with no todo line they go at the end, after adding a line ending to a last line that has none. New lines use the note's first line ending ("\n" when it has none). A document with no lines becomes '# Todo', a blank line, then the appended lines.</summary>
    /// <param name="document">The parsed note.</param>
    /// <param name="edits">The edits.</param>
    /// <returns>The new text.</returns>
    public static string Rebuild(NoteDocument document, IReadOnlyList<NoteLineEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);
        var eol = document.Lines.Count > 0 && document.Lines[0].Eol.Length > 0 ? document.Lines[0].Eol : "\n";
        var appended = edits.Where(static e => e.Kind == NoteLineEditKind.Append).Select(static e => e.Line!).ToList(); // why: Append always carries a line
        var changes = edits.Where(static e => e.Kind != NoteLineEditKind.Append).ToLookup(static e => e.Number);
        var anchor = document.Lines.ToList().FindLastIndex(static l => l.Todo is not null);
        var output = new List<(string Text, string Eol)>();
        if (document.Lines.Count == 0 && appended.Count > 0)
        {
            output.AddRange([("# Todo", eol), (string.Empty, eol)]);
        }

        for (var i = 0; i < document.Lines.Count; i++)
        {
            var line = document.Lines[i];
            AddEdited(output, line, changes[line.Number].LastOrDefault());
            if (i == anchor)
            {
                AddLines(output, appended, eol);
            }
        }

        if (anchor < 0)
        {
            AddLines(output, appended, eol);
        }

        return string.Concat(output.Select(static o => o.Text + o.Eol));
    }

    private static void AddEdited(List<(string Text, string Eol)> output, NoteLine line, NoteLineEdit? edit)
    {
        if (edit is not { Kind: NoteLineEditKind.Remove })
        {
            output.Add((edit?.Line ?? line.Text, line.Eol));
        }
    }

    private static void AddLines(List<(string Text, string Eol)> output, List<string> appended, string eol)
    {
        if (appended.Count > 0 && output.Count > 0 && output[^1].Eol.Length == 0)
        {
            output[^1] = (output[^1].Text, eol);
        }

        output.AddRange(appended.Select(line => (line, eol)));
    }

    private static string? SequenceAt(string text, int at)
    {
        foreach (var sequence in _escaped)
        {
            if (text.AsSpan(at).StartsWith(sequence, StringComparison.Ordinal))
            {
                return sequence;
            }
        }

        return null;
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
