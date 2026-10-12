using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The rules compiled with one match timeout, and the scan that finds their hits in one text.</summary>
internal sealed class CompiledRules
{
    /// <summary>The options every rule is compiled with; extra patterns are checked with the same ones.</summary>
    public const RegexOptions Always = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private readonly (string Id, Regex Regex)[] _rules;
    private readonly Regex _placeholder;

    public CompiledRules(IReadOnlyList<RedactionRule> rules, IReadOnlyList<string> ruleIds, TimeSpan matchTimeout)
    {
        MatchTimeout = matchTimeout;
        _rules = [.. rules.Select(r => (r.Id, new Regex(r.Pattern, r.Options | Always, matchTimeout)))];

        // Only a placeholder naming one of these rules is skipped, so a crafted "[REDACTED:<secret>]" is still scanned.
        var ids = string.Join('|', ruleIds.Append("blocked").Append("secret-field").Select(Regex.Escape));
        _placeholder = new Regex(@"\[REDACTED:(?:" + ids + @")\]", Always, matchTimeout);
    }

    /// <summary>The match timeout these rules were compiled with (the redactor's, scaled to the size tier).</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Every rule's hits in <paramref name="text"/>, outside existing placeholders; unsorted, may overlap.</summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="deadline">The whole-scrub budget, checked around the placeholder scan, before every
    /// rule's scan and after every match; null when the rules are all built in.</param>
    /// <exception cref="RegexMatchTimeoutException">A pattern ran past the match timeout, or the scan past the deadline.</exception>
    public List<RawHit> Find(string text, ScanDeadline? deadline)
    {
        // The deadline is checked around the placeholder scan, before every rule's scan and after every match, so the
        // regex work between two checks is one match call (bounded by the match timeout) or the built-in, linear
        // placeholder scan, whether a rule matches or not.
        deadline?.Check();
        var placeholders = new Placeholders(_placeholder.Matches(text));
        deadline?.Check();
        var raw = new List<RawHit>();
        for (var order = 0; order < _rules.Length; order++)
        {
            deadline?.Check();
            Collect(order, text, placeholders, deadline, raw);
        }

        deadline?.Check();
        return raw;
    }

    // One rule's hits: the value group v when it matched, otherwise the whole match; empty spans and spans inside a
    // placeholder are skipped.
    private void Collect(int order, string text, Placeholders placeholders, ScanDeadline? deadline, List<RawHit> raw)
    {
        var (id, regex) = _rules[order];
        foreach (Match match in regex.Matches(text))
        {
            deadline?.Check();
            var value = match.Groups["v"];
            var span = value.Success ? value : match;
            var end = span.Index + span.Length;
            if (span.Length > 0 && !placeholders.Contain(span.Index, end))
            {
                raw.Add(new RawHit(id, span.Index, end, order));
            }
        }
    }
}
