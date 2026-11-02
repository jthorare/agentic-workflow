namespace Aw.Context.Core.Map;

/// <summary>What a build's structure cut replaced (spec 2026-10-27 §5): the migration line of the build report.</summary>
/// <param name="Features">Feature nodes dropped to be regenerated (home, group and cluster ones).</param>
/// <param name="Links">File links dropped to be regenerated ('implements' and 'tags' edges of the last build).</param>
/// <param name="Undone">Decisions whose target is gone and that were undone.</param>
/// <param name="Carried">Accepted or rejected file links re-expressed on the new 'tags' edge.</param>
public sealed record StructureCounts(int Features, int Links, int Undone, int Carried)
{
    /// <summary>Nothing replaced.</summary>
    public static StructureCounts None { get; } = new(0, 0, 0, 0);
}
