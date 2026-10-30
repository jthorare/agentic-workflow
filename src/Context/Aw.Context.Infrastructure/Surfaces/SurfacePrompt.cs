using Aw.Context.Core.Models;
using Aw.Context.Core.Surfaces;
using Aw.Context.Core.Trust;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>The question Jev gets for one group and the reading of its answer (spec §6.9 "One item", "The options", "When Jev can't decide"). What comes back is untrusted, so only an offered option counts.</summary>
internal static class SurfacePrompt
{
    internal const int PathLength = 200;
    internal const int EvidencePerCandidate = 6;
    internal const int EdgesPerCandidate = 6;

    /// <summary>The group as a question: key = the group's key, a prompt listing each scrubbed candidate inside a block it calls data, and the numbered options 'separate', one 'merge as' per candidate, 'reject' (2 to 5, distinct by construction).</summary>
    /// <param name="group">The group.</param>
    /// <param name="egress">Scrubs every name, path and edge.</param>
    /// <returns>The question.</returns>
    internal static ChoiceQuestion Question(SurfaceGroup group, IEgressScrubber egress) => throw new NotSupportedException();

    /// <summary>Reads one answer by the option's position: separate, a merge with the candidate's own name (cleaned), or reject; anything missing, off the options or under the confidence bar is an undecided separate.</summary>
    /// <param name="group">The group that was asked.</param>
    /// <param name="question">The question that was sent (its options are what the answer must equal).</param>
    /// <param name="answer">Jev's answer, or null when there was none.</param>
    /// <returns>The verdict.</returns>
    internal static SurfaceVerdict Verdict(SurfaceGroup group, ChoiceQuestion question, ChoiceAnswer? answer) => throw new NotSupportedException();
}
