namespace Aw.Sindri.Features.Scoping;

/// <summary>What the rules of one run share: the facts and the ids citations resolve to.</summary>
/// <param name="facts">The facts of the run.</param>
/// <param name="challengeCap">The last round the challenge loop allows (the running recipe's loops.challenge.cap).</param>
/// <param name="gapCap">The last round the gap search loop allows (the running recipe's loops.gapsearch.cap).</param>
internal sealed class ScopeContext(ScopeFacts facts, int challengeCap, int gapCap)
{
    /// <summary>The last round the challenge loop allows; a found last round warns instead of failing once that many ran.</summary>
    internal int ChallengeCap { get; } = challengeCap;

    /// <summary>The last round the gap search loop allows; a confirmed last round warns instead of failing once that many ran.</summary>
    internal int GapCap { get; } = gapCap;

    /// <summary>The facts.</summary>
    internal ScopeFacts Facts { get; } = facts;

    /// <summary>The ids a citation can resolve to.</summary>
    internal ScopeIds Ids { get; } = new(facts);
}
