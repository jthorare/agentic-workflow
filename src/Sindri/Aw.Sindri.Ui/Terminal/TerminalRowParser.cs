using System.Text;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>Turns one line of capture-pane -e text into cells. The pen belongs to the frame: tmux writes a colour left open at a line's end into the next line too, so the caller passes the same pen for every line of a frame.</summary>
internal static class TerminalRowParser
{
    private const char Esc = '\u001b';
    private const char Bell = '\u0007';

    /// <summary>Parses one line.</summary>
    /// <param name="line">The line, SGR codes inline.</param>
    /// <param name="pen">The frame's pen: read for each cell and changed by each colour sequence.</param>
    /// <returns>The cells, as many as the line fills (wide characters take two). Not padded to the width.</returns>
    internal static List<TerminalCell> Parse(string line, TerminalPen pen)
    {
        var row = new List<TerminalCell>(line.Length);
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == Esc)
            {
                i = SkipEscape(line, i, pen);
                continue;
            }

            _ = Rune.TryGetRuneAt(line, i, out var rune);
            var text = line.Substring(i, rune.Utf16SequenceLength);
            i += rune.Utf16SequenceLength;
            Add(row, pen, text, TerminalCharWidth.Of(rune));
        }

        return row;
    }

    private static void Add(List<TerminalCell> row, TerminalPen pen, string text, int columns)
    {
        if (columns < 0)
        {
            return;
        }

        if (columns == 0)
        {
            Join(row, text);
            return;
        }

        row.Add(new TerminalCell(text, pen.Fg, pen.Bg, pen.Style, columns));
        if (columns == 2)
        {
            row.Add(new TerminalCell(string.Empty, pen.Fg, pen.Bg, pen.Style, 0));
        }
    }

    // A combining mark joins the cell before it; after the right half of a wide character, the left half.
    private static void Join(List<TerminalCell> row, string mark)
    {
        var last = row.Count - 1;
        if (last >= 0 && row[last].Columns == 0 && last > 0)
        {
            last--;
        }

        if (last >= 0)
        {
            row[last] = row[last] with { Text = row[last].Text + mark };
        }
    }

    // Returns the index after the escape sequence that starts at i; a colour sequence changes the pen.
    private static int SkipEscape(string s, int i, TerminalPen pen)
    {
        if (i + 1 >= s.Length)
        {
            return i + 1;
        }

        if (s[i + 1] == '[')
        {
            return SkipCsi(s, i, pen);
        }

        return s[i + 1] is ']' or 'P' or '_' or '^' or 'X' ? SkipString(s, i + 2) : i + 2;
    }

    private static int SkipCsi(string s, int i, TerminalPen pen)
    {
        var j = i + 2;
        while (j < s.Length && s[j] is >= (char)0x30 and <= (char)0x3F)
        {
            j++;
        }

        var end = j;
        while (j < s.Length && s[j] is >= (char)0x20 and <= (char)0x2F)
        {
            j++;
        }

        if (j >= s.Length)
        {
            return s.Length;
        }

        if (s[j] == 'm' && j == end)
        {
            pen.Apply(s.AsSpan(i + 2, end - i - 2));
        }

        return j + 1;
    }

    // A string sequence (OSC and friends) ends at BEL or ESC \.
    private static int SkipString(string s, int start)
    {
        for (var j = start; j < s.Length; j++)
        {
            if (s[j] == Bell)
            {
                return j + 1;
            }

            if (s[j] == Esc && j + 1 < s.Length && s[j + 1] == '\\')
            {
                return j + 2;
            }
        }

        return s.Length;
    }
}
