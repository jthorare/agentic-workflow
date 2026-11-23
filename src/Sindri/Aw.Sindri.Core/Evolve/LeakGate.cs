using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// The leak gate (privacy spec 2026-10-09 §5): rewrite, judge, terms, patterns, identifiers, verbatim, personal, scrubber, in that order;
/// the first hit holds. Pure: the model steps ran before it, and every list is passed in.
/// </summary>
/// <param name="scrubber">The secret scrubber; a rewrite it would change is held.</param>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The proposal_stage block (issue 3787, piece 3c) runs it; until then only the tests do.")]
internal sealed class LeakGate(ITextScrubber scrubber)
{
    /// <summary>Checks a rewrite before it may go public.</summary>
    /// <param name="input">The original, the rewrite, the judge's answer and the sources.</param>
    /// <param name="rules">The deny lists and private names.</param>
    /// <returns>Clean, or the first layer that held it.</returns>
    public LeakVerdict Check(GateInput input, PrivacyRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        var text = Text(input.Rewrite);
        (LeakLayer Layer, Func<bool> Hits)[] layers =
        [
            (LeakLayer.Rewrite, () => !KeepsIdentity(input.Original, input.Rewrite)),
            (LeakLayer.Judge, () => input.JudgeFoundLeak),
            (LeakLayer.Terms, () => TermsLayer.Hits(text, rules.DenyTerms)),
            (LeakLayer.Patterns, () => PatternsLayer.Hits(text, rules.DenyPatterns)),
            (LeakLayer.Identifiers, () => IdentifiersLayer.Hits(text, rules.PrivateNames, rules.CommonWords)),
            (LeakLayer.Verbatim, () => VerbatimLayer.Hits(text, input.Sources)),
            (LeakLayer.Personal, () => PersonalLayer.Hits(text)),
            (LeakLayer.Scrubber, () => Changes(text)),
        ];
        foreach (var (layer, hits) in layers)
        {
            if (hits())
            {
                return LeakVerdict.HeldBy(layer);   // why: the first hit holds; later layers never run
            }
        }

        return LeakVerdict.Clean;
    }

    // why: every field that would go public; the evidence refs are opaque and checked by KeepsIdentity instead
    private static string Text(Proposal proposal) =>
        string.Join('\n', [proposal.Target, proposal.Title, proposal.Rationale, proposal.Change.Description, .. proposal.Change.Files]);

    // why: the rewrite may change words only; a new kind, target or evidence set would make it a different proposal
    private static bool KeepsIdentity(Proposal original, Proposal rewrite) =>
        original.Kind == rewrite.Kind
        && string.Equals(original.Target, rewrite.Target, StringComparison.Ordinal)
        && original.Evidence.ToHashSet(StringComparer.Ordinal).SetEquals(rewrite.Evidence);

    private bool Changes(string text) =>
        !string.Equals(scrubber.Scrub(text), text, StringComparison.Ordinal)
        || !string.Equals(scrubber.Scrub(PrivacyText.Visible(text)), PrivacyText.Visible(text), StringComparison.Ordinal);
}
