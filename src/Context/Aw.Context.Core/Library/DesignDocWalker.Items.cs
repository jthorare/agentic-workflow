using System.Text;

namespace Aw.Context.Core.Library;

internal sealed partial class DesignDocWalker
{
    private readonly HashSet<string> _slugs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _slugRepeats = new(StringComparer.Ordinal);

    private DesignItem ToItem(Candidate c)
    {
        var b = _blocks[c.Block];
        var last = LastLine(b, c.Block);
        var joined = new StringBuilder();
        for (var i = b.Index; i <= last && joined.Length <= DesignDocParser.MaxTextChars + 256; i++)
        {
            _ = joined.Append(lines[i]).Append('\n');
        }

        var text = joined.ToString().Trim();
        if (text.Length > DesignDocParser.MaxTextChars)
        {
            var cut = char.IsHighSurrogate(text[DesignDocParser.MaxTextChars - 1]) ? DesignDocParser.MaxTextChars - 1 : DesignDocParser.MaxTextChars;
            text = text[..cut];
        }

        return new DesignItem(c.Key, c.Kind, c.Parent, b.Index + 1, last + 1, text);
    }

    private int LastLine(Block b, int bi)
    {
        var limit = b.Kind == BlockKind.TableRow ? b.Index : EndBefore(b, bi);
        while (limit > b.Index && lines[limit].Trim().Length == 0)
        {
            limit--;
        }

        return limit;
    }

    private int EndBefore(Block b, int bi)
    {
        for (var next = bi + 1; next < _blocks.Count; next++)
        {
            var n = _blocks[next];
            var ends = b.Kind == BlockKind.Heading
                ? n.Kind == BlockKind.Heading && n.Level <= b.Level
                : n.Kind != BlockKind.ListItem || n.Level <= b.Level;
            if (ends)
            {
                return n.Index - 1;
            }
        }

        return lines.Count - 1;
    }

    private string NextSlug(string slug)
    {
        var candidate = slug;
        var n = _slugRepeats.GetValueOrDefault(slug);
        while (_slugs.Contains(candidate))
        {
            n++;
            candidate = slug + "-" + FrontMatterBlock.Number(n);
        }

        _slugRepeats[slug] = n;
        _ = _slugs.Add(candidate);
        return candidate;
    }
}
