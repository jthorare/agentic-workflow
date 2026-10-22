using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.ranker' (Phase 8). Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class RankerRules
{
    /// <summary>The settings of a section; a missing section or key is its default (both stages off, equal weights, a list of 30).</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The settings.</returns>
    internal static RankerSettings Read(RankerSection? section) => new()
    {
        Features = section?.Features ?? false,
        Rerank = section?.Rerank ?? false,
        RerankTop = section?.RerankTop ?? RankerSettings.DefaultRerankTop,
        Weights = Weights(section?.Weights),
    };

    private static FeatureWeights Weights(RankerWeightsSection? weights) => Rest(weights) with
    {
        CascadeDistance = weights?.CascadeDistance ?? 1,
        EdgeType = weights?.EdgeType ?? 1,
    };

    private static FeatureWeights Rest(RankerWeightsSection? weights) => new()
    {
        Provenance = weights?.Provenance ?? 1,
        Supersession = weights?.Supersession ?? 1,
        Recency = weights?.Recency ?? 1,
    };

    /// <summary>The first value out of range, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Problem(RankerSection? section)
    {
        if (section?.RerankTop is < 1 or > RankerSettings.MaxRerankTop)
        {
            return SettingsReader.Invalid("context.ranker.rerankTop", "is 1 to 50 items");
        }

        var weights = section?.Weights;
        (string Key, double? Value)[] named =
        [
            ("cascadeDistance", weights?.CascadeDistance), ("edgeType", weights?.EdgeType), ("provenance", weights?.Provenance), ("supersession", weights?.Supersession), ("recency", weights?.Recency),
        ];
        return named.Where(static w => w.Value is < 0 or > FeatureWeights.MaxWeight or double.NaN)
            .Select(static w => SettingsReader.Invalid("context.ranker.weights." + w.Key, "is 0 to 10 (0 turns the feature off)")).FirstOrDefault();
    }
}
