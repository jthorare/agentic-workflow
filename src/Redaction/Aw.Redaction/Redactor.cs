using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>The built-in pattern set (spec §4c "One pattern source").</summary>
public sealed class Redactor : IRedactor
{
    // The match timeout is a budget per window of input: a text of up to Window characters gets the redactor's
    // timeout, and each doubling of the text doubles it, so input size alone never blocks; only a pathological match
    // does (spec §4c). The rules are compiled once per size tier, on first use.
    private const int Window = 128 * 1024;
    private const int TierCount = 24; // a safe upper bound: an int length needs at most tier 14

    private readonly IReadOnlyList<RedactionRule> _rules;
    private readonly TimeSpan _matchTimeout;
    private readonly CompiledRules?[] _tiers = new CompiledRules?[TierCount];
    private readonly Regex _secretName;
    private readonly TimeProvider? _clock;

    /// <summary>A redactor over <paramref name="rules"/> (tests inject a slow rule to prove the timeout blocks). With a
    /// <paramref name="clock"/>, a whole scrub also gets a deadline (<see cref="ScanBudgetFactor"/>).</summary>
    internal Redactor(IReadOnlyList<RedactionRule> rules, TimeSpan matchTimeout, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matchTimeout, TimeSpan.Zero);
        _rules = rules;
        _clock = clock;
        _matchTimeout = Cap(matchTimeout.TotalMilliseconds);
        RuleIds = [.. rules.Select(r => r.Id).Distinct(StringComparer.Ordinal)];
        RulesDigest = Digest(rules);
        _secretName = new Regex(
            BuiltinRules.SecretName,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
            _matchTimeout);
    }

    /// <summary>With extra patterns, a whole scrub may take this many times its per-pattern match timeout.</summary>
    public const int ScanBudgetFactor = 4;

    /// <summary>The per-pattern match timeout <see cref="Default"/> uses.</summary>
    public static TimeSpan DefaultMatchTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>A redactor over the built-in rules with <see cref="DefaultMatchTimeout"/>.</summary>
    public static Redactor Default { get; } = new(BuiltinRules.All, DefaultMatchTimeout);

    /// <summary>The rule ids, in priority order (an earlier rule names an overlapping hit).</summary>
    public IReadOnlyList<string> RuleIds { get; }

    /// <summary>
    /// A fingerprint of what the rules match: SHA-256 (64 lowercase hex characters) over the scan engine's version, the
    /// homoglyph table, the options every rule gets and every rule's id, pattern and options in order, each part
    /// length-prefixed. It changes whenever a rule does, even under the same id, so a caller
    /// that stores scrubbed text can tell it was scrubbed under other rules (the context engine cleans such text again).
    /// </summary>
    public string RulesDigest { get; }

    /// <summary>
    /// The scan engine's version, part of <see cref="RulesDigest"/>: raise it with any change to how a text is scanned
    /// beyond the rules themselves (FoldedText's folding and escapes, the invisible characters, how hits merge, the
    /// placeholder skip, the 'v' group). The homoglyph table and the options every rule gets are hashed as they are.
    /// </summary>
    internal const int EngineVersion = 1;

    private static string Digest(IReadOnlyList<RedactionRule> rules)
    {
        var text = new StringBuilder()
            .Append(System.Globalization.CultureInfo.InvariantCulture, $"engine:{EngineVersion}:{(int)CompiledRules.Always}\n")
            .Append(Homoglyphs.Pairs.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(Homoglyphs.Pairs).Append('\n');
        foreach (var rule in rules)
        {
            var options = ((int)rule.Options).ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var part in (string[])[rule.Id, rule.Pattern, options])
            {
                _ = text.Append(part.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(part).Append('\n');
            }
        }

        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>Creates a redactor over the built-in rules with its own per-pattern match timeout.</summary>
    /// <param name="matchTimeout">The timeout for each pattern; must be positive.</param>
    /// <returns>The redactor.</returns>
    public static Redactor Create(TimeSpan matchTimeout) => new(BuiltinRules.All, matchTimeout);

    /// <summary>
    /// Creates a redactor over the built-in rules and <paramref name="extraPatterns"/> from configuration, each
    /// validated against Redaction's pattern rules and compiled with <paramref name="matchTimeout"/>; a match that
    /// runs past it blocks, like a built-in rule's. Configured patterns aren't reviewed to stay linear, so with at least
    /// one of them a whole scrub also blocks once it runs past <see cref="ScanBudgetFactor"/> times its match timeout
    /// on <paramref name="clock"/>.
    /// </summary>
    /// <param name="extraPatterns">The configured patterns, after the built-ins in priority order.</param>
    /// <param name="matchTimeout">The timeout for each pattern; must be positive.</param>
    /// <param name="clock">The clock for the whole-scrub deadline (the host's TimeProvider).</param>
    /// <returns>The redactor, or every refused field with its entry index.</returns>
    public static RedactorResult Create(IReadOnlyList<ExtraPattern> extraPatterns, TimeSpan matchTimeout, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(extraPatterns);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matchTimeout, TimeSpan.Zero);
        // Capped like the redactor's own timeout, so a long one is accepted here too instead of thrown by Regex.
        var errors = ExtraPatternRules.Check(extraPatterns, Default.RuleIds, Cap(matchTimeout.TotalMilliseconds));
        return errors.Count > 0
            ? RedactorResult.Invalid(errors)
            : RedactorResult.Valid(new Redactor([.. BuiltinRules.All, .. extraPatterns.Select(e => new RedactionRule(e.Id, e.Pattern))], matchTimeout, extraPatterns.Count > 0 ? clock : null));
    }

    /// <inheritdoc/>
    public bool IsSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return (_secretName.IsMatch(name) && !SecretNameTokens.EndsInCountedToken(name)) || SecretNameTokens.Holds(name);
    }

    /// <inheritdoc/>
    public ScrubResult Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var hits = Merge(FindBoth(text))
                .Select(h => new RedactionHit(h.Rule, h.Start, h.End))
                .ToList();
            return hits.Count == 0
                ? new ScrubResult(text, [], ScrubOutcome.Clean, BlockCode: null)
                : new ScrubResult(Replace(text, hits), hits, ScrubOutcome.Redacted, BlockCode: null);
        }
        catch (RegexMatchTimeoutException)
        {
            // A timeout blocks; it never passes the text on (spec §4c). The caller logs the outcome.
            return new ScrubResult(ScrubResult.BlockedText, [], ScrubOutcome.Blocked, RedactionCodes.MatchTimeout);
        }
    }

    // The rules run on the text as written and on its folded copy, and the hits are united in original coordinates.
    // The folded pass sees through look-alikes, zero-width characters and JSON escapes; the plain pass keeps the
    // token boundaries the fold would change (Cyrillic prose glued to a token, a fullwidth letter before it, or a
    // Windows path whose backslash-r the fold would decode).
    private List<RawHit> FindBoth(string text)
    {
        var rules = RulesFor(text.Length);
        ScanDeadline? deadline = _clock is null
            ? null
            : new ScanDeadline(_clock, _clock.GetTimestamp(), rules.MatchTimeout * ScanBudgetFactor);
        var raw = rules.Find(text, deadline);
        if (text.AsSpan().ContainsAnyExceptInRange('\u0000', '\u007F') || text.Contains('\\', StringComparison.Ordinal))
        {
            var folded = FoldedText.Fold(text);
            raw.AddRange(rules.Find(folded.Text, deadline).Select(h =>
            {
                var (start, end) = folded.ToOriginal(h.Start, h.End);
                return h with { Start = start, End = end };
            }));
        }

        return raw;
    }

    /// <summary>The per-pattern match timeout used to scrub a text of <paramref name="length"/> characters.</summary>
    internal TimeSpan TimeoutFor(int length) => RulesFor(length).MatchTimeout;

    private CompiledRules RulesFor(int length)
    {
        var tier = TierFor(length);
        return _tiers[tier] ??= new CompiledRules(_rules, RuleIds, MatchTimeoutFor(_matchTimeout, length));
    }

    /// <summary>The per-pattern match timeout for a text of <paramref name="length"/> characters: <paramref name="baseTimeout"/>
    /// up to one window, doubled for each doubling of the text, capped like every timeout.</summary>
    internal static TimeSpan MatchTimeoutFor(TimeSpan baseTimeout, int length) =>
        Cap(baseTimeout.TotalMilliseconds * (1L << TierFor(length)));

    private static int TierFor(int length)
    {
        var tier = 0;
        while (tier < TierCount - 1 && (long)Window << tier < length)
        {
            tier++;
        }

        return tier;
    }

    // Regex refuses a timeout past int.MaxValue - 1 ms (about 24.8 days); a longer budget is capped there.
    private static TimeSpan Cap(double milliseconds) => TimeSpan.FromMilliseconds(Math.Min(milliseconds, int.MaxValue - 1));

    private static string Replace(string text, List<RedactionHit> hits)
    {
        var output = new StringBuilder(text.Length);
        var at = 0;
        foreach (var hit in hits)
        {
            _ = output.Append(text, at, hit.Start - at).Append("[REDACTED:").Append(hit.Rule).Append(']');
            at = hit.End;
        }

        return output.Append(text, at, text.Length - at).ToString();
    }

    // Sorted by start, then rule priority; overlapping hits merge into the earlier one, which keeps its rule.
    private static List<RawHit> Merge(List<RawHit> raw)
    {
        raw.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Order.CompareTo(b.Order));
        var merged = new List<RawHit>(raw.Count);
        foreach (var hit in raw)
        {
            if (merged.Count > 0 && hit.Start < merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, hit.End) };
            }
            else
            {
                merged.Add(hit);
            }
        }

        return merged;
    }
}
