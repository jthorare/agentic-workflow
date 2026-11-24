using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// Reads mrkdwn into spans (messages spec §6.3). Every span covers an exact slice of the source and writes that slice back, so Parse then Render is the
/// same text. Line rules (a quote, a list line) apply at the start of a line; a ``` block may run over lines; the rest stays inside one line. Nesting stops
/// at <see cref="MaxDepth"/>, and closers are found by a binary search over positions read once, so hostile or very long text stays near linear.
/// </summary>
internal sealed partial class MrkdwnParser
{
    /// <summary>How deep marks, quotes and list lines nest before the rest stays text.</summary>
    internal const int MaxDepth = 8;

    private const int MaxIndent = 8;

    private readonly string _s;

    private MrkdwnParser(string source)
    {
        _s = source;
        ReadPositions();
    }

    /// <summary>Parses the text into spans.</summary>
    /// <param name="text">The mrkdwn text.</param>
    /// <returns>The spans, in order.</returns>
    internal static IReadOnlyList<MrkdwnSpan> Parse(string text)
    {
        var parser = new MrkdwnParser(text);
        var spans = new SpanList(text);
        parser.Blocks(spans, 0, 0);
        return spans.Done();
    }

    // [start, the end of the text) line by line: a line rule at a line start, else inline spans up to the line's end (a ``` block may carry past it)
    private void Blocks(SpanList spans, int start, int depth)
    {
        var end = _s.Length;
        var i = start;
        while (i < end)
        {
            var lineEnd = LineEnd(i, end);
            if (AtLineStart(i) && TryLine(i, lineEnd, depth, out var line, out var next))
            {
                spans.Add(line);
                i = next;
            }
            else
            {
                i = Inline(spans, i, new Scope(lineEnd, end, depth));
            }

            if (i == lineEnd && i < end)
            {
                spans.Text(i, i + 1);
                i++;
            }
        }
    }

    private bool TryLine(int i, int lineEnd, int depth, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var end = _s.Length;
        span = null;
        next = end;
        if (depth >= MaxDepth)
        {
            return false;
        }

        // '>>>' quotes the rest of the message; '>' one line (Slack sends both escaped, '&gt;')
        var marker = Marker(i, lineEnd, ">>>", "&gt;&gt;&gt;");
        if (marker > 0)
        {
            var rest = new SpanList(_s);
            Blocks(rest, i + marker, depth + 1);
            span = new MrkdwnSpan(MrkdwnKind.Quote, _s[(i + marker)..end]) { Marker = _s[i..(i + marker)], Children = rest.Done() };
            return true;
        }

        var kind = MrkdwnKind.Quote;
        marker = Marker(i, lineEnd, ">", "&gt;");
        if (marker == 0)
        {
            kind = MrkdwnKind.ListItem;
            marker = ListMarker(i, lineEnd);
        }

        next = lineEnd;
        span = marker > 0 ? LineSpan(kind, i, i + marker, lineEnd, depth) : null;
        return span is not null;
    }

    private MrkdwnSpan LineSpan(MrkdwnKind kind, int start, int content, int lineEnd, int depth) =>
        new(kind, _s[content..lineEnd]) { Marker = _s[start..content], Children = InlineSpans(content, lineEnd, depth + 1) };

    // the length of a quote mark at i, with one space after it, or 0
    private int Marker(int i, int lineEnd, string plain, string escaped)
    {
        var length = Starts(i, lineEnd, plain) ? plain.Length : Starts(i, lineEnd, escaped) ? escaped.Length : 0;
        return length > 0 && i + length < lineEnd && _s[i + length] == ' ' ? length + 1 : length;
    }

    // the length of a list marker at i ('  • ', '- ', '* ', '12. ', '3) '), with its indent and the space after it, or 0
    private int ListMarker(int i, int lineEnd)
    {
        var j = i;
        while (j < lineEnd && j - i < MaxIndent && _s[j] is ' ' or '\t')
        {
            j++;
        }

        var after = j < lineEnd ? BulletEnd(j, lineEnd) : -1;
        return after > 0 && after < lineEnd && _s[after] == ' ' ? after + 1 - i : 0;
    }

    private int BulletEnd(int j, int lineEnd)
    {
        if (_s[j] is '•' or '◦' or '▪' or '-' or '*')
        {
            return j + 1;
        }

        var d = j;
        while (d < lineEnd && d - j < 3 && char.IsAsciiDigit(_s[d]))
        {
            d++;
        }

        return d > j && d < lineEnd && _s[d] is '.' or ')' ? d + 1 : -1;
    }

    private List<MrkdwnSpan> InlineSpans(int start, int end, int depth)
    {
        var spans = new SpanList(_s);
        _ = Inline(spans, start, new Scope(end, end, depth));
        return spans.Done();
    }

    // inline spans over [start, scope.End); a ``` block may end as late as scope.PreEnd. Returns where it stopped: the end, or past a block that ran over it
    private int Inline(SpanList spans, int start, Scope scope)
    {
        var i = start;
        var text = start;
        while (i < scope.End)
        {
            if (TryToken(i, scope, out var span, out var next))
            {
                spans.Text(text, i);
                spans.Add(span);
                i = next;
                text = next;
            }
            else
            {
                i++;
            }
        }

        spans.Text(text, scope.End);
        return i;
    }

    private bool AtLineStart(int i) => i == 0 || _s[i - 1] == '\n';

    private int LineEnd(int i, int end)
    {
        var newline = _s.IndexOf('\n', i, end - i);
        return newline < 0 ? end : newline;
    }

    private bool Starts(int i, int end, string mark) => _s.AsSpan(i, end - i).StartsWith(mark, StringComparison.Ordinal);

    /// <summary>Where inline tokens may reach: the end of the range, the end a ``` block may reach, and the nesting depth.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Scope(int End, int PreEnd, int Depth);

    /// <summary>The spans so far, with the text between tokens kept as one run until the next span.</summary>
    private sealed class SpanList(string source)
    {
        private readonly List<MrkdwnSpan> _spans = [];
        private int _from = -1;
        private int _to;

        internal void Text(int from, int to)
        {
            if (to <= from)
            {
                return;
            }

            if (_from >= 0 && from != _to)
            {
                Flush();
            }

            _from = _from < 0 ? from : _from;
            _to = to;
        }

        internal void Add(MrkdwnSpan span)
        {
            Flush();
            _spans.Add(span);
        }

        internal List<MrkdwnSpan> Done()
        {
            Flush();
            return _spans;
        }

        private void Flush()
        {
            if (_from >= 0)
            {
                _spans.Add(new MrkdwnSpan(MrkdwnKind.Text, source[_from.._to]));
                _from = -1;
            }
        }
    }
}
