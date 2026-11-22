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
    public LeakVerdict Check(GateInput input, PrivacyRules rules) => throw new NotSupportedException(scrubber.GetType().Name);
}
