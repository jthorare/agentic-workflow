namespace Aw.Context.Core.Eval.Backtest;

/// <summary>A backtest arm (spec §9.3): which inputs a question is answered from.</summary>
public enum Arm
{
    /// <summary>The brief-only baseline: the planner's text-anchor step on the brief against the as-of map, no source, no cascade.</summary>
    Brief,

    /// <summary>The repo docs at the cut commit, no graph.</summary>
    Docs,

    /// <summary>The as-of map's graph (the floor).</summary>
    Graph,

    /// <summary>The current map with every review decision (the ceiling, labeled leaky).</summary>
    GraphReviewed,

    /// <summary>The native Linear source (S-6).</summary>
    Linear,

    /// <summary>The Onyx source (S-7B).</summary>
    Onyx,

    /// <summary>The code source (S-9).</summary>
    Code,

    /// <summary>Every available input: the as-of graph and the repo docs at the cut.</summary>
    All,

    /// <summary>The ranker comparison's baseline (S-8): every input, fused by reciprocal rank fusion alone.</summary>
    RankerRrf,

    /// <summary>The ranker comparison's second row (S-8): every input, fusion plus the feature scores.</summary>
    RankerFeatures,

    /// <summary>The ranker comparison's third row (S-8): every input, fusion plus features plus the model rerank of the top items.</summary>
    RankerRerank,
}
