using System.Buffers;

namespace Aw.Redaction;

/// <summary>One clause of a <see cref="RuleGate"/>: the text holds at least one of these literals.</summary>
internal sealed class RuleGateClause
{
    private readonly SearchValues<string>? _strings;
    private readonly SearchValues<char>? _chars;

    private RuleGateClause(string[] literals, bool caseless, bool chars)
    {
        Literals = literals;
        Caseless = caseless;
        if (chars)
        {
            _chars = SearchValues.Create(string.Concat(literals));
        }
        else
        {
            _strings = SearchValues.Create(literals, caseless ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
    }

    /// <summary>The literals, one of which must be in the text.</summary>
    public IReadOnlyList<string> Literals { get; }

    /// <summary>True when the literals are compared ignoring ASCII case.</summary>
    public bool Caseless { get; }

    /// <summary>One of <paramref name="literals"/>, exactly as written. In a rule compiled with IgnoreCase, only for
    /// literals without letters (the tests check it).</summary>
    public static RuleGateClause Exact(params string[] literals) => new(literals, caseless: false, chars: false);

    /// <summary>One of <paramref name="chars"/>, exactly as written (a set of letterless characters, such as digits).</summary>
    public static RuleGateClause AnyChar(string chars) => new([.. chars.Select(c => c.ToString())], caseless: false, chars: true);

    /// <summary>One of <paramref name="literals"/> in any ASCII case, for a rule compiled with IgnoreCase. The regex's
    /// case-insensitive match also takes some non-ASCII letters (the Kelvin sign for k), so on a text with any
    /// non-ASCII character this clause always holds.</summary>
    public static RuleGateClause AnyCase(params string[] literals)
    {
        if (literals.Any(l => l.AsSpan().ContainsAnyExceptInRange(' ', '~')))
        {
            throw new ArgumentException("A caseless gate literal must be printable ASCII.", nameof(literals));
        }

        return new(literals, caseless: true, chars: false);
    }

    /// <summary>False only when no literal is in <paramref name="text"/>.</summary>
    public bool MayHold(ReadOnlySpan<char> text, bool ascii) =>
        _chars is not null
            ? text.ContainsAny(_chars)
            : (Caseless && !ascii) || text.ContainsAny(_strings!);   // why: the constructor sets _strings whenever _chars is null
}
