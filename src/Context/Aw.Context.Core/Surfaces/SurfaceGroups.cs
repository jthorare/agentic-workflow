namespace Aw.Context.Core.Surfaces;

/// <summary>Builds groups from a set of lookalike candidates (spec §6.9).</summary>
public static class SurfaceGroups
{
    /// <summary>Cuts lookalikes into groups of <see cref="SurfaceGroup.MaxCandidates"/> in node-key order: 7 give 3, 3 and 1. A merge across two groups is not offered in this pass.</summary>
    /// <param name="lookalikes">Candidates that look like the same thing.</param>
    /// <returns>The groups, in key order.</returns>
    public static IReadOnlyList<SurfaceGroup> Cut(IEnumerable<SurfaceCandidate> lookalikes)
    {
        ArgumentNullException.ThrowIfNull(lookalikes);
        var ordered = lookalikes.OrderBy(static c => c.NodeKey, StringComparer.Ordinal).ThenBy(static c => c.Name, StringComparer.Ordinal).ToList();
        return [.. ordered.Chunk(SurfaceGroup.MaxCandidates).Select(static chunk => new SurfaceGroup(chunk))];
    }
}
