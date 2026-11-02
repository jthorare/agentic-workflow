namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>What the grouper found.</summary>
/// <param name="Groups">The kept groups, ordered by first member's record key.</param>
/// <param name="EdgeCosines">The cosine of every nearest-neighbour pair before the threshold, each pair once.</param>
public sealed record GroupingResult(IReadOnlyList<ConceptGroup> Groups, IReadOnlyList<float> EdgeCosines);
