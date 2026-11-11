using Aw.Redaction;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Infrastructure.Text;

/// <summary>The secret finder over Aw.Redaction. A hit's span is the value group when the rule has one (every secret-assignment rule does), so the span text is the value.</summary>
/// <param name="redactor">The redactor.</param>
internal sealed class RedactingSecretFinder(IRedactor redactor) : ISecretFinder
{
    public IReadOnlyList<SecretHit> Find(string text, out bool complete)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = redactor.Scrub(text);
        complete = !result.IsBlocked;
        return [.. result.Hits.Select(h => new SecretHit(h.Rule, text[h.Start..h.End]))];
    }
}
