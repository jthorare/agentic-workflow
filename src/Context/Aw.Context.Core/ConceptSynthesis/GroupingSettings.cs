namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>The knobs of the concept grouper.</summary>
/// <param name="Neighbours">How many nearest records each record is compared with.</param>
/// <param name="MinCosine">The cosine two records need to be joined.</param>
/// <param name="MaxGroup">The most members a group may hold; a bigger one is split at a tighter cosine.</param>
/// <param name="MinRecords">The fewest members a group needs to be kept.</param>
/// <param name="MinSources">The fewest distinct sources a group needs to be kept.</param>
/// <param name="SplitStep">How much the cosine rises on each split round.</param>
public sealed record GroupingSettings(int Neighbours = 8, float MinCosine = 0.75f, int MaxGroup = 20, int MinRecords = 2, int MinSources = 1, float SplitStep = 0.05f);
