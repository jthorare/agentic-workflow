using System.Globalization;
using System.Text;

namespace Aw.Context.Core.Pages;

/// <summary>Reads a pasted page list into blocks: blank lines separate pages, the first line of a block is the page name, every other line an action.</summary>
public static class PageListText
{
    private const string Arrow = "->";
    private const string Bullets = "-*•+";

    /// <summary>Splits the paste into blocks. Control and format characters go (a tab becomes a space), a line loses its leading bullet or number, a line that is empty after that is skipped.</summary>
    /// <param name="text">The paste.</param>
    /// <returns>The blocks in paste order, numbered from 1.</returns>
    public static IReadOnlyList<PageListBlock> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var blocks = new List<PageListBlock>();
        var open = new Open();
        var lineNumber = 0;
        foreach (var raw in Clean(text).Split('\n'))
        {
            lineNumber++;
            if (raw.Trim().Length == 0)
            {
                open.Close(blocks);
            }
            else if (Unmarked(raw.Trim()) is { Length: > 0 } line)
            {
                open.Add(line, lineNumber);
            }
        }

        open.Close(blocks);
        return blocks;
    }

    private sealed class Open
    {
        private readonly List<PageListAction> _actions = [];
        private string? _name;
        private int _line;

        internal void Add(string line, int lineNumber)
        {
            if (_name is null)
            {
                _name = line;
                _line = lineNumber;
            }
            else
            {
                _actions.Add(Action(line));
            }
        }

        internal void Close(List<PageListBlock> blocks)
        {
            if (_name is not null)
            {
                blocks.Add(new PageListBlock(blocks.Count + 1, _line, _name, [.. _actions]));
            }

            _name = null;
            _actions.Clear();
        }
    }

    private static string Clean(string text)
    {
        var clean = new StringBuilder(text.Length);
        foreach (var ch in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
        {
            if (ch is '\n' or ' ')
            {
                clean.Append(ch);
            }
            else if (ch == '\t')
            {
                clean.Append(' ');
            }
            else if (!char.IsControl(ch) && char.GetUnicodeCategory(ch) != UnicodeCategory.Format)
            {
                clean.Append(ch);
            }
        }

        return clean.ToString();
    }

    // A leading bullet, or digits and a '.' or ')' followed by a space (so '1.5 hours' stays), and the spaces after it.
    private static string Unmarked(string line)
    {
        if (Bullets.Contains(line[0], StringComparison.Ordinal))
        {
            return line[1..].TrimStart();
        }

        var digits = 0;
        while (digits < line.Length && line[digits] is >= '0' and <= '9')
        {
            digits++;
        }

        var marked = digits > 0 && digits < line.Length && line[digits] is '.' or ')' && (digits + 1 == line.Length || line[digits + 1] == ' ');
        return marked ? line[(digits + 1)..].TrimStart() : line;
    }

    private static PageListAction Action(string line)
    {
        var arrow = line.IndexOf(Arrow, StringComparison.Ordinal);
        if (arrow < 0)
        {
            return new PageListAction(line, []);
        }

        var concepts = line[(arrow + Arrow.Length)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new PageListAction(line[..arrow].Trim(), concepts);
    }
}
