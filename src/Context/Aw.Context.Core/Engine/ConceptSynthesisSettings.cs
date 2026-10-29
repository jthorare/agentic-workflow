using Aw.Context.Core.ConceptSynthesis;

namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptSynthesis' (#1597): the pass that groups the whole record store by meaning and has a model name each group. On unless the profile turns it off, and only when labeling is on too (it sends record text to a model).</summary>
public sealed record ConceptSynthesisSettings
{
    /// <summary>Whether the index-decisions run synthesizes concepts.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>How many nearest records each record is compared with.</summary>
    public int Neighbours { get; init; } = 8;

    /// <summary>The cosine two records need to be joined.</summary>
    public float MinCosine { get; init; } = 0.85f;

    /// <summary>The most records a group may hold; a bigger one is split at a tighter cosine.</summary>
    public int MaxGroup { get; init; } = 20;

    /// <summary>The fewest records a group needs to be kept.</summary>
    public int MinRecords { get; init; } = 2;

    /// <summary>The fewest distinct sources a group needs to be kept.</summary>
    public int MinSources { get; init; } = 1;

    /// <summary>The most characters of one record's text a prompt carries.</summary>
    public int MaxCharsPerRecord { get; init; } = 1_500;

    /// <summary>The most characters one prompt carries.</summary>
    public int MaxCharsPerCall { get; init; } = 30_000;

    /// <summary>The share of a derived concept's features one synthesized concept must hold for the derived one to fold into it (#1281).</summary>
    public double CoverShare { get; init; }

    /// <summary>The grouper's knobs.</summary>
    public GroupingSettings Grouping => new(Neighbours, MinCosine, MaxGroup, MinRecords, MinSources);
}
