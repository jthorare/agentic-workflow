namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Buckets cosines so a threshold can be chosen by eye.</summary>
public static class CosineHistogram
{
    /// <summary>Counts cosines per bucket.</summary>
    /// <param name="cosines">The cosines.</param>
    /// <param name="width">The bucket width.</param>
    /// <returns>The non-empty buckets as (lower bound, count), lowest first.</returns>
    public static IReadOnlyList<(float Lower, int Count)> Buckets(IReadOnlyList<float> cosines, float width = 0.05f)
    {
        ArgumentNullException.ThrowIfNull(cosines);
        return cosines
            .GroupBy(c => (int)MathF.Floor(c / width))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key * width, g.Count()))
            .ToList();
    }
}
