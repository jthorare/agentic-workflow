using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Messages;

/// <summary>The inline tokens: marks, code, ``` blocks, links and mentions, emoji names.</summary>
internal sealed partial class MrkdwnParser
{
    private const int MaxEmojiName = 100;

    // positions read once: where each mark may close, every backtick, and every start of '```'
    private readonly List<int> _boldClosers = [];
    private readonly List<int> _italicClosers = [];
    private readonly List<int> _strikeClosers = [];
    private readonly List<int> _ticks = [];
    private readonly List<int> _fences = [];

    private void ReadPositions()
    {
        for (var j = 0; j < _s.Length; j++)
        {
            var c = _s[j];
            if (c == '`')
            {
                _ticks.Add(j);
                if (Starts(j, _s.Length, "```"))
                {
                    _fences.Add(j);
                }
            }
            else if (c is '*' or '_' or '~' && ClosesMark(j))
            {
                Closers(c).Add(j);
            }
        }
    }

    private bool TryToken(int i, Scope scope, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        switch (_s[i])
        {
            case '<':
                return TryAngle(i, scope.End, out span, out next);
            case '`':
                return TryFence(i, scope.PreEnd, out span, out next) || TryCode(i, scope.End, out span, out next);
            case '*' or '_' or '~':
                return TryMark(i, scope.End, scope.Depth, out span, out next);
            case ':':
                return TryEmoji(i, scope.End, out span, out next);
            default:
                return None(i, out span, out next);
        }
    }

    // *bold*, _italic_, ~strike~: opens after a non-word character before a non-space, closes on the first valid closer in the same range
    private bool TryMark(int i, int end, int depth, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var mark = _s[i];
        var opens = depth < MaxDepth && (i == 0 || !char.IsLetterOrDigit(_s[i - 1])) && i + 1 < end && !char.IsWhiteSpace(_s[i + 1]) && _s[i + 1] != mark;
        var close = opens ? NextAt(Closers(mark), i + 2) : -1;
        if (close < 0 || close >= end)
        {
            return None(i, out span, out next);
        }

        var kind = mark switch { '*' => MrkdwnKind.Bold, '_' => MrkdwnKind.Italic, _ => MrkdwnKind.Strike };
        span = new MrkdwnSpan(kind, _s[(i + 1)..close]) { Children = InlineSpans(i + 1, close, depth + 1) };
        next = close + 1;
        return true;
    }

    // `code`: literal up to the next backtick on the line
    private bool TryCode(int i, int end, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var close = NextAt(_ticks, i + 1);
        if (close <= i + 1 || close >= end)
        {
            return None(i, out span, out next);
        }

        span = new MrkdwnSpan(MrkdwnKind.Code, _s[(i + 1)..close]);
        next = close + 1;
        return true;
    }

    // ```block```: literal up to the next '```', which may be lines later
    private bool TryFence(int i, int preEnd, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var close = Starts(i, preEnd, "```") ? NextAt(_fences, i + 3) : -1;
        if (close <= i + 3 || close + 3 > preEnd)
        {
            return None(i, out span, out next);
        }

        span = new MrkdwnSpan(MrkdwnKind.Pre, _s[(i + 3)..close]);
        next = close + 3;
        return true;
    }

    // <url|label>, <@U…>, <#C…|name>, <!here>: up to the next '>' on the line, never across another '<'
    private bool TryAngle(int i, int end, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var close = _s.AsSpan(i + 1, end - i - 1).IndexOfAny('<', '>');
        span = close > 0 && _s[i + 1 + close] == '>' ? AngleSpan(_s.Substring(i + 1, close)) : null;
        next = i + close + 2;
        return span is not null;
    }

    // :name: between non-word characters
    private bool TryEmoji(int i, int end, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        var j = i + 1;
        while (j < end && j - i <= MaxEmojiName && IsEmojiChar(_s[j]))
        {
            j++;
        }

        var closes = j > i + 1 && j < end && _s[j] == ':' && (j + 1 == _s.Length || !char.IsLetterOrDigit(_s[j + 1]));
        if (!closes || (i > 0 && char.IsLetterOrDigit(_s[i - 1])))
        {
            return None(i, out span, out next);
        }

        span = new MrkdwnSpan(MrkdwnKind.Emoji, _s[(i + 1)..j]);
        next = j + 1;
        return true;
    }

    private static MrkdwnSpan? AngleSpan(string inner)
    {
        var bar = inner.IndexOf('|', StringComparison.Ordinal);
        var head = bar < 0 ? inner : inner[..bar];
        var label = bar < 0 ? null : inner[(bar + 1)..];
        var (kind, target) = head switch
        {
            ['@', ..] => (MrkdwnKind.UserMention, head[1..]),
            ['#', ..] => (MrkdwnKind.ChannelMention, head[1..]),
            "!here" or "!channel" or "!everyone" => (MrkdwnKind.Broadcast, head[1..]),
            _ when IsLinkTarget(head) => (MrkdwnKind.Link, head),
            _ => (MrkdwnKind.Text, string.Empty),
        };

        return kind == MrkdwnKind.Text || target.Length == 0 || target.Any(char.IsWhiteSpace)
            ? null
            : new MrkdwnSpan(kind, label ?? target) { Target = target, Label = label };
    }

    // only web and mail links open on a click (spec §11); any other scheme stays text
    private static bool IsLinkTarget(string head) =>
        head.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || head.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || head.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private static bool IsEmojiChar(char c) => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '+' or '-';

    private static int NextAt(List<int> positions, int from)
    {
        var at = positions.BinarySearch(from);
        at = at < 0 ? ~at : at;
        return at < positions.Count ? positions[at] : -1;
    }

    private static bool None(int i, [NotNullWhen(true)] out MrkdwnSpan? span, out int next)
    {
        span = null;
        next = i;
        return false;
    }

    private bool ClosesMark(int j) => j > 0 && !char.IsWhiteSpace(_s[j - 1]) && (j + 1 == _s.Length || !char.IsLetterOrDigit(_s[j + 1]));

    private List<int> Closers(char mark) => mark switch
    {
        '*' => _boldClosers,
        '_' => _italicClosers,
        _ => _strikeClosers,
    };
}
