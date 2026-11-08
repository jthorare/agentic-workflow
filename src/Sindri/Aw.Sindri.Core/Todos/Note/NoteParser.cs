namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Reads the Obsidian todo note (todo spec §4.5). Pure: no clock, no file.</summary>
public static class NoteParser
{
    private const int MaxFenceIndent = 3;
    private const int MinFenceLength = 3;

    /// <summary>Reads a note. A top-level '- [ ]', '- [x]' or '- [X]' line is a todo; task lines in a fenced block (backtick or tilde), in a quote or callout ('&gt;') or indented are not, and neither is any other check state ('[/]', '[-]'). Every other line is kept as it is.</summary>
    /// <param name="note">The note's text (any mix of "\n" and "\r\n").</param>
    /// <returns>The document.</returns>
    public static NoteDocument Parse(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var lines = new List<NoteLine>();
        var fence = default(Fence);
        var at = 0;
        while (at < note.Length)
        {
            var newline = note.IndexOf('\n', at);
            var end = newline < 0 ? note.Length : newline;
            var crlf = newline > at && note[newline - 1] == '\r';
            var text = note[at..(crlf ? end - 1 : end)];
            var eol = newline < 0 ? string.Empty : crlf ? "\r\n" : "\n";
            at = newline < 0 ? note.Length : newline + 1;

            var number = lines.Count + 1;
            fence = fence.Next(text, out var inFence);
            var todo = !inFence && NoteTodoLineReader.IsTaskLine(text) ? NoteTodoLineReader.Read(number, text) : null;
            lines.Add(new NoteLine(number, text, eol, todo));
        }

        return new NoteDocument(lines);
    }

    /// <summary>The open fenced block, if any: its fence character and length.</summary>
    private readonly record struct Fence(char Mark, int Length)
    {
        public Fence Next(string line, out bool inFence)
        {
            if (Mark != '\0')
            {
                inFence = true;
                return Closes(line) ? default : this;
            }

            var open = Opens(line);
            inFence = open.Mark != '\0';
            return open;
        }

        private static Fence Opens(string line)
        {
            var indent = Indent(line);
            if (indent > MaxFenceIndent || indent >= line.Length || line[indent] is not ('`' or '~'))
            {
                return default;
            }

            var run = Run(line, indent, line[indent]);
            var info = line.AsSpan(indent + run);
            return run >= MinFenceLength && !(line[indent] == '`' && info.Contains('`')) ? new Fence(line[indent], run) : default;
        }

        private bool Closes(string line)
        {
            var indent = Indent(line);
            return indent <= MaxFenceIndent
                && Run(line, indent, Mark) >= Length
                && string.IsNullOrWhiteSpace(line[(indent + Run(line, indent, Mark))..]);
        }

        private static int Indent(string line)
        {
            var n = 0;
            while (n < line.Length && line[n] == ' ')
            {
                n++;
            }

            return n;
        }

        private static int Run(string line, int from, char mark)
        {
            var n = 0;
            while (from + n < line.Length && line[from + n] == mark)
            {
                n++;
            }

            return n;
        }
    }
}
