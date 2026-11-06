using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Library;

internal sealed partial class DesignDocWalker
{
    private static readonly string[] _noCells = [];

    private enum BlockKind
    {
        Heading,
        ListItem,
        TableRow,
    }

    private sealed record Block(BlockKind Kind, int Index, int Level, string Content, IReadOnlyList<string> Cells);

    private sealed class FenceState
    {
        private char _char;
        private int _length;

        /// <summary>Whether a line is a fence line or inside a fence (and so not read), tracking opens and closes.</summary>
        internal bool Skips(string line)
        {
            if (_char != '\0')
            {
                var t = line.Trim();
                if (t.Length >= _length && t.All(c => c == _char))
                {
                    _char = '\0';
                }

                return true;
            }

            var open = FenceOpen.Match(line);
            if (open.Success)
            {
                _char = open.Groups["fence"].Value[0];
                _length = open.Groups["fence"].Length;
            }

            return open.Success;
        }
    }

    private List<Block> Scan()
    {
        var blocks = new List<Block>();
        var fence = new FenceState();
        for (var i = bodyStart; i < lines.Count; i++)
        {
            if (fence.Skips(lines[i]))
            {
                continue;
            }

            var block = Classify(i);
            if (block is not null)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private Block? Classify(int i)
    {
        var line = lines[i];
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '>')
        {
            return null;
        }

        var heading = Heading.Match(line);
        if (heading.Success)
        {
            return new Block(BlockKind.Heading, i, heading.Groups["hashes"].Length, heading.Groups["text"].Value.Trim(), _noCells);
        }

        if (trimmed[0] == '|')
        {
            var cells = Cells(trimmed);
            return cells.Count > 0 && !cells.TrueForAll(static c => Separator.IsMatch(c)) ? new Block(BlockKind.TableRow, i, 0, cells[0], cells) : null;
        }

        var list = ListMarker.Match(line);
        return list.Success ? new Block(BlockKind.ListItem, i, list.Groups["indent"].Value.Sum(static c => c == '\t' ? 4 : 1), list.Groups["content"].Value, _noCells) : null;
    }

    private static List<string> Cells(string row)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inCode = false;
        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];
            if (c == '\\' && i + 1 < row.Length && row[i + 1] == '|')
            {
                _ = cell.Append('|');
                i++;
            }
            else if (c == '|' && !inCode)
            {
                cells.Add(cell.ToString().Trim());
                _ = cell.Clear();
            }
            else
            {
                inCode ^= c == '`';
                _ = cell.Append(c);
            }
        }

        if (cell.ToString().Trim().Length > 0)
        {
            cells.Add(cell.ToString().Trim());
        }

        cells.RemoveAt(0);
        return cells;
    }

    [GeneratedRegex("^[ \\t]*(?<fence>`{3,}|~{3,})", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FenceOpen { get; }

    [GeneratedRegex("^ {0,3}(?<hashes>#{1,6})[ \\t]+(?<text>.*?)(?:[ \\t]+#+)?[ \\t]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Heading { get; }

    [GeneratedRegex("^(?<indent>[ \\t]*)(?:[-*]|[0-9]{1,9}[.)])[ \\t]+(?<content>.*)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListMarker { get; }

    [GeneratedRegex("^(?<key>reqs|depends)[ \\t]*:[ \\t]*(?<rest>.*)\\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyLine { get; }

    [GeneratedRegex("^:?-+:?\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Separator { get; }
}
