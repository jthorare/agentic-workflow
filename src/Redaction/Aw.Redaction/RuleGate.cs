namespace Aw.Redaction;

/// <summary>
/// A necessary condition for one rule to match, checked before its regex runs: every clause must find one of its
/// literals in the scanned text, or the regex can't match and is skipped. A gate may only name what every match of
/// its pattern must contain (a literal of the pattern, or one of a set of alternatives that covers every branch); a
/// gate that misses a match would leak a secret, so a rule without a gate always runs, and the tests check each gate
/// against its regex (RuleGateTests).
/// </summary>
internal sealed class RuleGate
{
    private readonly RuleGateClause[] _clauses;

    /// <summary>A gate over <paramref name="clauses"/>; all must hold.</summary>
    public RuleGate(params RuleGateClause[] clauses) => _clauses = clauses;

    /// <summary>The clauses, all of which must hold.</summary>
    public IReadOnlyList<RuleGateClause> Clauses => _clauses;

    /// <summary>False only when the rule's regex can't match <paramref name="text"/>.</summary>
    /// <param name="text">The text the rule would scan.</param>
    /// <param name="ascii">True when <paramref name="text"/> holds only ASCII characters.</param>
    public bool MayMatch(ReadOnlySpan<char> text, bool ascii)
    {
        foreach (var clause in _clauses)
        {
            if (!clause.MayHold(text, ascii))
            {
                return false;
            }
        }

        return true;
    }
}
