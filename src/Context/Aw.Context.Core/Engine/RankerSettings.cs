namespace Aw.Context.Core.Engine;

/// <summary>'context.ranker' (spec §8.2 steps 5 and 6, Phase 8): the two ranker stages, each off until a backtest arm shows it helps (or the owner decides).</summary>
public sealed record RankerSettings
{
    /// <summary>The widest rerank list the profile may ask for.</summary>
    public const int MaxRerankTop = 50;

    /// <summary>'context.ranker.features': whether the feature scores re-order the fused items. Off by default.</summary>
    public bool Features { get; init; }

    /// <summary>'context.ranker.weights': the weight of each feature; equal by default.</summary>
    public Ranking.FeatureWeights Weights { get; init; } = new();

    /// <summary>'context.ranker.rerank': whether a model orders the top items (the only model call on the query path). Off by default.</summary>
    public bool Rerank { get; init; }

    /// <summary>'context.ranker.rerankTop' (1 to 50, default 30): how many of the best items the rerank orders.</summary>
    public int RerankTop { get; init; } = Ranking.RerankOrder.DefaultTop;
}
