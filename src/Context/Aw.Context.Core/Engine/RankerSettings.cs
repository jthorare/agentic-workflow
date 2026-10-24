namespace Aw.Context.Core.Engine;

/// <summary>'context.ranker' (spec §8.2 steps 5 and 6, Phase 8): the two ranker stages, each off until a backtest arm shows it helps (or the owner decides).</summary>
public sealed record RankerSettings
{
    /// <summary>The widest rerank list the profile may ask for.</summary>
    public const int MaxRerankTop = 100;

    /// <summary>How many of the best items the rerank orders when the profile doesn't say.</summary>
    public const int DefaultRerankTop = 30;

    /// <summary>'context.ranker.features': whether the feature scores re-order the fused items. On by default (pure compute, no model call; owner q93, 2026-10-23).</summary>
    public bool Features { get; init; } = true;

    /// <summary>'context.ranker.weights': the weight of each feature; equal by default.</summary>
    public FeatureWeights Weights { get; init; } = new();

    /// <summary>'context.ranker.rerank': whether a model orders the top items (the only model call on the query path). Off by default.</summary>
    public bool Rerank { get; init; }

    /// <summary>'context.ranker.rerankTop' (1 to 100, default 30): how many of the best items the rerank orders.</summary>
    public int RerankTop { get; init; } = DefaultRerankTop;
}
