using Aw.Redaction;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Infrastructure.Text;

/// <summary>The secret finder over Aw.Redaction. A hit's span is the value group when the rule has one (every secret-assignment rule does), so the span text is the value and the name before it is the key.</summary>
/// <param name="redactor">The redactor.</param>
internal sealed class RedactingSecretFinder(IRedactor redactor) : ISecretFinder
{
    public IReadOnlyList<SecretHit> Find(string text, out bool complete)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = redactor.Scrub(text);
        complete = !result.IsBlocked;
        return [.. result.Hits.Select(h => new SecretHit(h.Rule, KeyBefore(text, h.Start), text[h.Start..h.End]))];
    }

    // The name before a value: skip back over quotes and spaces, one '=' or ':', quotes and spaces again, then take the name characters.
    internal static string KeyBefore(string text, int start)
    {
        var i = SkipBack(text, start);
        if (i == 0 || text[i - 1] is not ('=' or ':'))
        {
            return string.Empty;
        }

        var end = SkipBack(text, i - 1);
        var from = end;
        while (from > 0 && (char.IsAsciiLetterOrDigit(text[from - 1]) || text[from - 1] is '_' or '-' or '.'))
        {
            from--;
        }

        return text[from..end];
    }

    private static int SkipBack(string text, int i)
    {
        while (i > 0 && text[i - 1] is ' ' or '\t' or '\r' or '\n' or '\f' or '\v' or '"' or '\'')
        {
            i--;
        }

        return i;
    }
}
