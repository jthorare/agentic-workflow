using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>What the /rootCause investigation handoff says.</summary>
/// <param name="Status">The handoff's status line (diagnosed, unfixed, …).</param>
/// <param name="Hypotheses">The hypotheses table.</param>
/// <param name="RootCause">The Root Cause section's text; empty when the section is missing.</param>
public sealed record Handoff(string Status, IReadOnlyList<Hypothesis> Hypotheses, string RootCause)
{
    /// <summary>
    /// Whether the handoff supports a fix: diagnosed, a root cause, and either a confirmed hypothesis or at least two open
    /// ones (an unconfirmed cause is fixed by competing implementers, mode B, never by a single one).
    /// </summary>
    /// <returns>The handoff, or AW-BUGFIX-006.</returns>
    public Result<Handoff> SupportsFix()
    {
        if (Status != "diagnosed")
        {
            return BugFixErrors.HandoffRefused.With($"handoff status is \"{Status}\", expected \"diagnosed\" (run /rootCause --investigate-only)");
        }

        if (!Hypotheses.Any(static h => h.Result == HypothesisResult.Confirmed) && !Hypotheses.Where(static h => h.Result != HypothesisResult.RuledOut).Skip(1).Any())
        {
            return BugFixErrors.HandoffRefused.With("no hypothesis is confirmed and fewer than 2 are open; run /rootCause again to confirm one or to list the competing candidates");
        }

        return RootCause.Length == 0 ? BugFixErrors.HandoffRefused.With("the handoff has no ## Root Cause text") : this;
    }
}
