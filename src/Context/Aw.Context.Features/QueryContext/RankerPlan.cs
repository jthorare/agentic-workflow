using Aw.Context.Core.Engine;
using Aw.Context.Core.Ranking;

namespace Aw.Context.Features.QueryContext;

/// <summary>Which ranker stages one query runs: the profile's, or a backtest arm's.</summary>
/// <param name="Features">Whether the feature scores re-order the items.</param>
/// <param name="Weights">The feature weights.</param>
/// <param name="Rerank">Whether a model orders the top items.</param>
/// <param name="Top">How many items the rerank orders.</param>
internal sealed record RankerPlan(bool Features, FeatureWeights Weights, bool Rerank, int Top)
{
    /// <summary>The plan of <paramref name="settings"/>, or of <paramref name="mode"/> when a backtest arm names one (the weights and the list size stay the profile's).</summary>
    /// <param name="settings">The profile's ranker settings.</param>
    /// <param name="mode">The arm's mode, or null.</param>
    /// <returns>The plan.</returns>
    internal static RankerPlan For(RankerSettings settings, RankerMode? mode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return mode switch
        {
            null => new RankerPlan(settings.Features, settings.Weights, settings.Rerank, settings.RerankTop),
            RankerMode.Rrf => new RankerPlan(Features: false, settings.Weights, Rerank: false, settings.RerankTop),
            RankerMode.Features => new RankerPlan(Features: true, settings.Weights, Rerank: false, settings.RerankTop),
            RankerMode.Rerank => new RankerPlan(Features: true, settings.Weights, Rerank: true, settings.RerankTop),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
